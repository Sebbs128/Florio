using System.Net;
using System.Runtime.CompilerServices;

using Florio.Data;
using Florio.VectorEmbeddings.CosmosDb.Models;
using Florio.VectorEmbeddings.Extensions;
using Florio.VectorEmbeddings.Repositories;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

using Polly;

namespace Florio.VectorEmbeddings.CosmosDb;

public sealed class CosmosDbRepository(
    CosmosClient cosmosClient,
    EmbeddingsSettings settings,
    ILogger<CosmosDbRepository> logger)
    : IWordDefinitionRepository
{
    private readonly CosmosClient _cosmosClient = cosmosClient;
    private readonly EmbeddingsSettings _settings = settings;
    private readonly ILogger<CosmosDbRepository> _logger = logger;

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

    private Container GetContainer(CosmosClient client) => client.GetContainer(_settings.CollectionName, _settings.CollectionName);

    public async Task<bool> CollectionExists(CancellationToken cancellationToken = default)
    {
        return await _collectionExistsStartupPipeline.ExecuteAsync(async cancelToken =>
        {
            try
            {
                var containerResponse = await GetContainer(_cosmosClient)
                    .ReadContainerAsync(cancellationToken: cancellationToken);

                if (containerResponse is not { StatusCode: HttpStatusCode.Created or HttpStatusCode.OK })
                {
                    return false;
                }

                var container = containerResponse.Container;

                return container.GetItemLinqQueryable<VectorGroupDocument>().Count() > 73_000;
            }
            catch (CosmosException cosmosEx) when (cosmosEx is { StatusCode: HttpStatusCode.NotFound })
            {
                return false;
            }
        }, cancellationToken);
    }

    public async Task CreateCollection(int vectorSize, CancellationToken cancellationToken)
    {
        var dbResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_settings.CollectionName, cancellationToken: cancellationToken);

        var containerProperties = new ContainerProperties(_settings.CollectionName, PartitionKeyPath)
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

    public async IAsyncEnumerable<WordDefinition> FindClosestMatch(
        ReadOnlyMemory<float> vector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string queryText = """
            SELECT TOP 1 VectorDistance(v.vector, @targetVector) AS score, v.wordDefinitions
            FROM vectorGroupDocument v
            WHERE score > @threshold
            ORDER BY VectorDistance(v.vector, @targetVector)
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@targetVector", vector.ToArray())
            .WithParameter("@threshold", _settings.ScoreThreshold);

        using var feed = GetContainer(_cosmosClient)
            .GetItemQueryIterator<QueryResult>(query);

        while (feed.HasMoreResults && !cancellationToken.IsCancellationRequested)
        {
            var response = await feed.ReadNextAsync(cancellationToken);
            foreach (var result in response)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Nearest result to {vector} has similarity {similarity}", vector.ToSparseRepresentation(), result.score);
                }

                foreach (var item in result.wordDefinitions)
                {
                    yield return item;
                }
            }
        }
    }

    public async IAsyncEnumerable<WordDefinition> FindMatches(
        ReadOnlyMemory<float> vector,
        int limit = 10,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string queryText = """
            SELECT VectorDistance(v.vector, @targetVector) AS score, v.wordDefinitions
            FROM vectorGroupDocument v
            ORDER BY VectorDistance(v.vector, @targetVector)
            OFFSET 0 LIMIT @limit
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@targetVector", vector.ToArray())
            .WithParameter("@limit", limit);

        using var feed = GetContainer(_cosmosClient)
            .GetItemQueryIterator<QueryResult>(query);

        while (feed.HasMoreResults && !cancellationToken.IsCancellationRequested)
        {
            var response = await feed.ReadNextAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Search for {vector} found {count} results", vector.ToSparseRepresentation(), response.Count());
            }
            foreach (var result in response)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Result \"{word}\" has similarity score {score}", result.wordDefinitions[0].word, result.score);
                }

                foreach (var item in result.wordDefinitions)
                {
                    yield return item;
                }
            }
        }
    }

    public async IAsyncEnumerable<WordDefinition> FindByWord(
        ReadOnlyMemory<float> vector,
        int limit = 10,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string queryText = """
            SELECT TOP 10 VectorDistance(v.vector, @targetVector) AS score, v.wordDefinitions
            FROM vectorGroupDocument v
            ORDER BY VectorDistance(v.vector, @targetVector)
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@targetVector", vector.ToArray());

        using var feed = GetContainer(_cosmosClient)
            .GetItemQueryIterator<QueryResult>(query);

        while (feed.HasMoreResults && !cancellationToken.IsCancellationRequested)
        {
            var response = await feed.ReadNextAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Search for {vector} found {count} results", vector.ToSparseRepresentation(), response.Count());
            }
            foreach (var result in response)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Result \"{word}\" has similarity score {score}", result.wordDefinitions[0].word, result.score);
                }

                yield return result.wordDefinitions.First();
            }
        }
    }

    public async Task InsertBatch(
        IReadOnlyList<WordDefinitionEmbedding> values,
        CancellationToken cancellationToken = default)
    {
        var groupedByVector = values
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

        var container = GetContainer(_cosmosClient);
        int itemsCount = 0;

        var batchSize = 5;

        foreach (var batch in groupedByVector.Chunk(batchSize))
        {

            await _upsertBatchPipeline.ExecuteAsync(async cancelToken =>
            {
                await Task.WhenAll(batch.Select(i =>
                    container.UpsertItemAsync(i, new PartitionKey(i.partitionKey), cancellationToken: cancelToken)));
            }, cancellationToken);

            itemsCount += batch.Sum(i => i.wordDefinitions.Length);
            _logger.LogInformation("{RecordProgress} of {TotalRecords} added to collection.", itemsCount, values.Count);
        }

        //for (int batch = 0; batch < values.Count; batch += batchSize)
        //{
        //    var toAdd = values
        //        .Skip(batch)
        //        .Take(batchSize)
        //        .Select(v => new WordDefinitionEntity(
        //            (++itemsCount).ToString(),
        //            v.Vector.ToArray(),
        //            v.WordDefinition.Word[0].ToString(),
        //            v.WordDefinition.Word,
        //            v.WordDefinition.Definition,
        //            v.WordDefinition.ReferencedWords))
        //        .ToArray();

        //    await _upsertBatchPipeline.ExecuteAsync(async cancelToken =>
        //    {
        //        await Task.WhenAll(toAdd
        //            .Select(entity => container.UpsertItemAsync(entity, cancellationToken: cancellationToken)));
        //    }, cancellationToken);

        //    _logger.LogInformation("{RecordProgress} of {TotalRecords} added to collection.", itemsCount, values.Count);
        //}

        // update container to have index on Word column
        //var indexingPolicy = new IndexingPolicy();
        //indexingPolicy.IncludedPaths.Add(new IncludedPath { Path = $"/vectors/{nameof(WordDefinitionEntity.word)}/" });

        //await container.ReplaceContainerAsync(
        //    new ContainerProperties
        //    {
        //        Id = _settings.CollectionName,
        //        PartitionKeyPath = PartitionKeyPath,
        //        IndexingPolicy = indexingPolicy
        //    },
        //    cancellationToken: cancellationToken);
    }
}
