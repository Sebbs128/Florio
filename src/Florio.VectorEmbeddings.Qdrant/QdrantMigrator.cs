using System.Diagnostics;

using Florio.Data;
using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Repositories;

using Grpc.Core;

using Microsoft.Extensions.Logging;

using Polly;

using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Florio.VectorEmbeddings.Qdrant;

public sealed class QdrantMigrator(
    QdrantClient qdrantClient,
    IWordDefinitionParser textParser,
    IVectorEmbeddingModelFactory embeddingsModelFactory,
    IStringFormatter stringFormatter,
    EmbeddingsSettings settings,
    ILogger<QdrantMigrator> logger)
    : MigratorBase(textParser, embeddingsModelFactory, stringFormatter, logger)
{
    private readonly QdrantClient _qdrantClient = qdrantClient;
    private readonly EmbeddingsSettings _settings = settings;

    private static readonly ResiliencePipeline _qdrantStartingUpPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new Polly.Retry.RetryStrategyOptions
        {
            Delay = TimeSpan.FromSeconds(10),
            ShouldHandle = new PredicateBuilder().Handle<RpcException>(rpcException => rpcException.StatusCode == StatusCode.Unavailable),
            MaxRetryAttempts = 10
        })
        .Build();
    private static readonly ResiliencePipeline _upsertBatchPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new Polly.Retry.RetryStrategyOptions
        {
            Delay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(10),
            MaxRetryAttempts = 10,
            UseJitter = true
        })
        .Build();

    private class CollectionState
    {
        public string? CollectionName { get; set; }
        public string? AliasName { get; set; }
        public int VectorSize { get; set; }
        public int CollectionSize { get; set; }
    }

    public override async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching current Qdrant collection state.");

        var currentState = await GetCurrentState(cancellationToken);

        _logger.LogInformation("""
            Current state:
              Collection {collection}
              Alias {alias}
              Vector Size {dimensions}
              Vector Count {count}
            """,
            currentState.CollectionName,
            currentState.AliasName,
            currentState.VectorSize,
            currentState.CollectionSize);

        var model = _embeddingsModelFactory.GetModel();
        var targetState = new CollectionState
        {
            AliasName = _settings.CollectionName,
            VectorSize = model.CalculateVector("a").Length
        };

        _logger.LogInformation("""
            Target state:
              Collection {collection}
              Alias {alias}
              Vector Size {dimensions}
            """,
            targetState.CollectionName,
            targetState.AliasName,
            targetState.VectorSize);

        _logger.LogInformation("Running migration checks...");

        var requiresReseeding = false;

        if (currentState.AliasName is null || currentState.VectorSize != targetState.VectorSize)
        {
            requiresReseeding = true;
            // TODO: collection name suffix needs to be something that can be calculated to indicate properties of the collection
            targetState.CollectionName = $"{_settings.CollectionName}-{DateTime.UtcNow:yyyyMMdd_HHmm}";

            _logger.LogInformation("Creating new collection {collection}, with vector size {dimensions}.",
                targetState.CollectionName,
                targetState.VectorSize);
            await CreateCollection(targetState.VectorSize, targetState.CollectionName, cancellationToken);
        }

        if (requiresReseeding)
        {
            await ReseedCollection(targetState.CollectionName!, model, cancellationToken);
        }

        // if currentState.CollectionName is the same as targetState.AliasName, the alias can't be created
        // current { collection: null, alias: null } => new alias(name: settings, collection: target)
        // current { collection: settings, alias: null } => delete(current.collection), new alias(name: settings, collection: target)
        // current { collection: not settings, alias: not null } => delete(current.alias), new alias(name: settings, collection: target), delete(current.collection)
        if (!string.Equals(currentState.CollectionName, targetState.CollectionName) || currentState.AliasName is null)
        {
            if (currentState is { AliasName: not null })
            {
                _logger.LogInformation("Deleting existing alias {alias}.", currentState.AliasName);
                await _qdrantClient.DeleteAliasAsync(currentState.AliasName, cancellationToken: cancellationToken);
            }
            // if there currently isn't an alias, and the current collection name is what we want to name the alias
            // we need to drop the current collection before we can add an alias with that name
            else if (string.Equals(currentState.CollectionName, targetState.AliasName))
            {
                _logger.LogInformation("Deleting previous collection {collection}.", currentState.CollectionName);
                await _qdrantClient.DeleteCollectionAsync(currentState.CollectionName, cancellationToken: cancellationToken);
            }

            _logger.LogInformation("Creating new alias {alias} for collection {collection}.",
                targetState.AliasName,
                targetState.CollectionName);
            await _qdrantClient.CreateAliasAsync(targetState.AliasName!, targetState.CollectionName!, cancellationToken: cancellationToken);

            if (currentState is { CollectionName: not null, AliasName: not null })
            {
                _logger.LogInformation("Deleting previous collection {collection}.", currentState.CollectionName);
                await _qdrantClient.DeleteCollectionAsync(currentState.CollectionName, cancellationToken: cancellationToken);
            }
        }
    }

    private async Task<CollectionState> GetCurrentState(CancellationToken cancellationToken = default)
    {
        return await _qdrantStartingUpPipeline.ExecuteAsync(async cancelToken =>
        {
            try
            {
                var info = await _qdrantClient.GetCollectionInfoAsync(_settings.CollectionName, cancelToken);
                var aliasInfo = (await _qdrantClient.ListAliasesAsync(cancelToken))
                    .SingleOrDefault(alias => alias.AliasName.Equals(_settings.CollectionName));

                return new CollectionState()
                {
                    AliasName = aliasInfo?.AliasName,
                    CollectionName = aliasInfo?.CollectionName ?? _settings.CollectionName,
                    VectorSize = (int)info.Config.Params.VectorsConfig.Params.Size,
                    CollectionSize = (int)info.PointsCount
                };
            }
            catch (RpcException rpcException) when (rpcException.StatusCode == StatusCode.NotFound)
            {
                return new();
            }
        }, cancellationToken);
    }

    protected override async Task CreateCollection(int vectorSize, string collectionName, CancellationToken cancellationToken = default)
    {
        await _qdrantClient.CreateCollectionAsync(collectionName,
            new VectorParams
            {
                Distance = Distance.Cosine,
                Datatype = Datatype.Float32,
                Size = (ulong)vectorSize,
                HnswConfig = new HnswConfigDiff(),
            },
            cancellationToken: cancellationToken);
    }

    protected override async Task InsertRecords(string collectionName, IReadOnlyList<WordDefinitionEmbedding> records, CancellationToken cancellationToken = default)
    {
        const int batchSize = 2_000; // most useful for controlling amount of memory consumed while inserting to vector db

        int itemsCount = 1;
        for (var batch = 0; batch < records.Count; batch += batchSize)
        {
            var toAdd = records
                .Skip(batch)
                .Take(batchSize)
                .Select(v => new PointStruct()
                {
                    Id = (ulong)itemsCount++,
                    Vectors = v.Vector.ToArray(),
                    Payload =
                    {
                            [nameof(WordDefinition.Word)] = v.WordDefinition.Word,
                            [nameof(WordDefinition.Definition)] = v.WordDefinition.Definition,
                            [nameof(WordDefinition.ReferencedWords)] = v.WordDefinition.ReferencedWords ?? []
                    }
                })
                .ToArray();

            await _upsertBatchPipeline.ExecuteAsync(async cancelToken =>
            {
                var updateResult = await _qdrantClient.UpsertAsync(collectionName,
                    toAdd,
                    wait: true,
                    cancellationToken: cancelToken);
                Debug.Assert(updateResult.Status == UpdateStatus.Completed);
            }, cancellationToken);

            _logger.LogInformation("{RecordProgress} of {TotalRecords} added to collection.", itemsCount - 1, records.Count);

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);

            GC.Collect();
        }

        await _qdrantClient.CreatePayloadIndexAsync(collectionName, nameof(WordDefinition.Word),
            cancellationToken: cancellationToken);
    }
}
