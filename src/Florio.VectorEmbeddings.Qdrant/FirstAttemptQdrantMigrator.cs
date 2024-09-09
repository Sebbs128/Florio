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

public sealed class FirstAttemptQdrantMigrator(
    QdrantClient qdrantClient,
    IWordDefinitionParser textParser,
    IVectorEmbeddingModelFactory embeddingsModelFactory,
    IStringFormatter stringFormatter,
    EmbeddingsSettings settings,
    ILogger<QdrantMigrator> logger) : IRepositoryMigrator
{
    private readonly QdrantClient _qdrantClient = qdrantClient;
    private readonly IWordDefinitionParser _textParser = textParser;
    private readonly IVectorEmbeddingModelFactory _embeddingsModelFactory = embeddingsModelFactory;
    private readonly IStringFormatter _stringFormatter = stringFormatter;
    private readonly EmbeddingsSettings _settings = settings;
    private readonly ILogger<QdrantMigrator> _logger = logger;

    private static readonly ResiliencePipeline _collectionExistsStartupPipeline = new ResiliencePipelineBuilder()
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

    private class MigrationState
    {
        public string? ExistingCollectionName { get; set; }
        public string? NewCollectionName { get; set; }
        public string? AliasName { get; set; }
        public bool RequiresReseeding { get; set; }
        public int VectorSize { get; set; }
        public bool MoveAlias { get; set; }
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var model = _embeddingsModelFactory.GetModel();
        var state = new MigrationState
        {
            VectorSize = model.CalculateVector("a").Length
        };

        // if collection doesn't exist
        //   create collection
        //   reseed
        //   finish migrating
        if (!await CollectionExists(_settings.CollectionName, cancellationToken))
        {
            state.RequiresReseeding = true;
            state.NewCollectionName = $"{_settings.CollectionName}-yyyyMMdd_HHmm";
            await CreateCollection(state.VectorSize, state.NewCollectionName, cancellationToken);

            state.MoveAlias = true;
        }
        // otherwise
        //   if collection needs migrating
        //     update existing collection
        //       if no alias
        //         create new collection
        //     if reseeding required
        //       reseed
        //     finish migrating
        //       if no alias
        //         create new alias
        //       otherwise
        //         update alias
        //       delete old collection
        else
        {
            var currentCollection = (await _qdrantClient.ListAliasesAsync(cancellationToken))
                    .SingleOrDefault(alias => alias.AliasName.Equals(_settings.CollectionName));

            if (currentCollection is not null)
            {
                state.ExistingCollectionName = currentCollection.CollectionName;
                state.AliasName = currentCollection.AliasName;
            }
            else
            {
                // alias hasn't been created yet
                // can't name an alias the same as a collection
                // alternatively, wait to create the alias until the end when we can simultaneously delete the existing collection
                state.AliasName = $"{_settings.CollectionName}-alias";
                state.ExistingCollectionName = _settings.CollectionName;
                state.RequiresReseeding = true;
            }
        }

        if (state.RequiresReseeding)
        {
            await ReseedCollection(state.NewCollectionName, cancellationToken);
        }

        if (state.MoveAlias)
        {
            var targetCollectionName = state.NewCollectionName ?? state.ExistingCollectionName;

            // TODO: could wait to do this until the end in this situation
            await _qdrantClient.CreateAliasAsync(aliasName, currentCollectionName, cancellationToken: cancellationToken);
        }


        // consider:
        //   breaking steps up into classes
        //   generating a list of those classes
        //   passing some state object between each
    }

    private async Task<bool> CollectionExists(string collectionName, CancellationToken cancellationToken)
    {
        // during startup, Qdrant may take a moment to load the collection from disk
        return await _collectionExistsStartupPipeline.ExecuteAsync(async cancelToken =>
        {
            try
            {
                var info = await _qdrantClient.GetCollectionInfoAsync(collectionName, cancelToken);
                return info.Status == CollectionStatus.Green && info.HasIndexedVectorsCount;
            }
            catch (RpcException rpcException) when (rpcException.StatusCode == StatusCode.NotFound)
            {
                return false;
            }
        }, cancellationToken);
    }

    private async Task CreateCollection(int vectorSize, string collectionName, CancellationToken cancellationToken)
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

    private async Task ReseedCollection(string collectionName, CancellationToken cancellationToken)
    {
        var wordDefinitions = _textParser.ParseLines(cancellationToken);

        _logger.LogInformation("Populating vector database.");

        var records = await wordDefinitions
            .GroupBy(wd => _stringFormatter.NormalizeForVector(wd.Word))
            .SelectMany(wg =>
            {
                var key = model.CalculateVector(wg.Key);
                return wg.Select(wd => new WordDefinitionEmbedding(key, wd));
            })
            .ToListAsync(cancellationToken);

        try
        {
            await ReseedCollection(cancellationToken);

            VectorDbInitCompleted = true;
            _logger.LogInformation("Vector database populated.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to populate vector database.");
        }

        const int batchSize = 2_000; // most useful for controlling amount of memory consumed while inserting to vector db

        int itemsCount = 1;
        for (var batch = 0; batch < values.Count; batch += batchSize)
        {
            var toAdd = values
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
                var updateResult = await _qdrantClient.UpsertAsync(_settings.CollectionName,
                    toAdd,
                    wait: true,
                    cancellationToken: cancelToken);
                Debug.Assert(updateResult.Status == UpdateStatus.Completed);
            }, cancellationToken);

            _logger.LogInformation("{RecordProgress} of {TotalRecords} added to collection.", itemsCount - 1, values.Count);

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);

            GC.Collect();
        }
    }
}
