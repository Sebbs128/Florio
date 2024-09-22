using System.Net;

using Florio.Data;
using Florio.VectorEmbeddings.CosmosDb.Models;
using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Repositories;

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

using Polly;

namespace Florio.VectorEmbeddings.CosmosDb;
public sealed class CosmosDbMigrator(
    CosmosClient cosmosClient,
    IWordDefinitionParser textParser,
    IVectorEmbeddingModelFactory embeddingsModelFactory,
    IStringFormatter stringFormatter,
    EmbeddingsSettings settings,
    ILogger<CosmosDbMigrator> logger)
    : MigratorBase(textParser, embeddingsModelFactory, stringFormatter, logger)
{
    private readonly CosmosClient _cosmosClient = cosmosClient;
    private readonly EmbeddingsSettings _settings = settings;

    private static readonly ResiliencePipeline _collectionExistsStartupPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new Polly.Retry.RetryStrategyOptions
        {
            Delay = TimeSpan.FromSeconds(10),
            ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(),
            MaxRetryAttempts = 10
        })
        .Build();
    private static readonly ResiliencePipeline _upsertBatchPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new Polly.Retry.RetryStrategyOptions
        {
            Delay = TimeSpan.FromSeconds(1),
            ShouldHandle = new PredicateBuilder().Handle<CosmosException>(ex => ex.StatusCode == HttpStatusCode.RequestTimeout),
            MaxDelay = TimeSpan.FromSeconds(10),
            MaxRetryAttempts = 10,
            UseJitter = true
        })
        .Build();

    private const string PartitionKeyPath = "/partitionKey";

    private class ContainerState
    {
        public string? DatabaseName { get; set; }
        public string? ContainerName { get; set; }
        public int VectorSize { get; set; }
        public int CollectionSize { get; set; }
    }

    public override async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching current CosmosDb state.");

        var currentState = await GetCurrentState(cancellationToken);

        _logger.LogInformation("""
            Current state:
              Database: {database}
              Container: {container}
              Vector Size: {dimensions}
              Vector Count: {count}
            """,
            currentState.DatabaseName,
            currentState.ContainerName,
            currentState.VectorSize,
            currentState.CollectionSize);

        var model = _embeddingsModelFactory.GetModel();
        var targetState = new ContainerState
        {
            DatabaseName = _settings.CollectionName,
            ContainerName = _settings.CollectionName,
            CollectionSize = _settings.NumberOfVectors,
            VectorSize = _settings.VectorSize,
        };

        _logger.LogInformation("""
            Target state:
              Database {database}
              Container {container}
              Vector Size {dimensions}
              Vector Count: {count}
            """,
            currentState.DatabaseName,
            currentState.ContainerName,
            currentState.VectorSize,
            currentState.CollectionSize);

        _logger.LogInformation("Running migration checks...");

        var requiresReseeding = currentState.CollectionSize != targetState.CollectionSize
            || currentState.VectorSize != targetState.VectorSize;

        if (currentState is { DatabaseName: null } or { ContainerName: null } || currentState.VectorSize != targetState.VectorSize)
        {
            requiresReseeding = true;
            _logger.LogInformation("Creating new CosmosDb database and container {name}, with vector size {dimensions}",
                targetState.ContainerName,
                targetState.VectorSize);
            await CreateCollection(targetState.VectorSize, targetState.ContainerName, cancellationToken);
        }

        if (requiresReseeding)
        {
            await ReseedCollection(targetState.ContainerName!, model, cancellationToken);
        }
    }

    private async Task<ContainerState> GetCurrentState(CancellationToken cancellationToken)
    {
        return await _collectionExistsStartupPipeline.ExecuteAsync(async cancelToken =>
        {
            var container = _cosmosClient.GetContainer(_settings.CollectionName, _settings.CollectionName);

            if (container is null)
            {
                return new ContainerState();
            }

            try
            {
                var properties = await container.ReadContainerAsync(cancellationToken: cancelToken);

                return new ContainerState
                {
                    DatabaseName = container.Database.Id,
                    ContainerName = container.Id,
                    VectorSize = (int)(properties.Resource.VectorEmbeddingPolicy.Embeddings.FirstOrDefault()?.Dimensions ?? 0),
                    CollectionSize = await container.GetItemLinqQueryable<WordDefinitionDocument>().CountAsync(cancelToken)
                };
            }
            catch (CosmosException cosmosEx) when (cosmosEx.StatusCode == HttpStatusCode.NotFound)
            {
                return new ContainerState();
            }
        });
    }

    protected override async Task CreateCollection(int vectorSize, string collectionName, CancellationToken cancellationToken = default)
    {
        var dbResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(collectionName, cancellationToken: cancellationToken);

        var containerProperties = new ContainerProperties(collectionName, PartitionKeyPath)
        {
            VectorEmbeddingPolicy = new(new(
                [
                    new()
                    {
                        Path = $"/{nameof(VectorGroupDocument.vector)}",
                        DataType = VectorDataType.Float32,
                        DistanceFunction = DistanceFunction.Cosine,
                        Dimensions = (ulong)vectorSize
                    }
                ])),
            IndexingPolicy = new()
            {
                VectorIndexes = new()
                {
                    new()
                    {
                        Path = $"/{nameof(VectorGroupDocument.vector)}",
                        Type = VectorIndexType.QuantizedFlat,
                    }
                }
            }
        };
        containerProperties.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });
        containerProperties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = $"/{nameof(VectorGroupDocument.vector)}/*" });
        containerProperties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/_etag/?" });

        await dbResponse.Database.CreateContainerIfNotExistsAsync(containerProperties, cancellationToken: cancellationToken);

    }

    protected override async Task InsertRecords(string collectionName, IReadOnlyList<WordDefinitionEmbedding> records, CancellationToken cancellationToken = default)
    {
        var vectorComparer = EqualityComparer<float[]>.Create((x, y) =>
            (x, y) switch
            {
                (null, null) => true,
                (null, _) or (_, null) => false,
                _ => x.SequenceEqual(y)
            });

        var groupedByVector = records
            .GroupBy(v => v.Vector)
            .Select(vg => new VectorGroupDocument(
                id: Guid.NewGuid().ToString("N"),
                vector: vg.Key.ToArray(),
                partitionKey: vg.First().WordDefinition.Word[0].ToString(),
                wordDefinitions: vg
                    .Select(v => v.WordDefinition)
                    .Select(wd => new WordDefinitionDocument(
                        wd.Word,
                        wd.Definition,
                        wd.ReferencedWords))
                    .ToArray()));

        var container = _cosmosClient.GetContainer(collectionName, collectionName);
        int itemsCount = 0;

        var initialThroughput = await container.ReadThroughputAsync(cancellationToken);

        var batchSize = 5;// Math.Max(initialThroughput ?? 400 / 40, 5);

        foreach (var item in groupedByVector.Chunk(batchSize))
        {
            await _upsertBatchPipeline.ExecuteAsync(async cancelToken =>
            {
                await Task.WhenAll(item.Select(i => container.UpsertItemAsync(i, cancellationToken: cancelToken)));
            }, cancellationToken);
            itemsCount += item.Sum(i => i.wordDefinitions.Length);
            _logger.LogInformation("{RecordProgress} of {TotalRecords} added to collection.", itemsCount, records.Count);
        }
    }
}
