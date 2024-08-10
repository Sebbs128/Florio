using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Florio.Data;
using Florio.VectorEmbeddings.Repositories;

using Google.Protobuf.Collections;

using Grpc.Core;

using Microsoft.Extensions.Logging;

using Polly;

using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Florio.VectorEmbeddings.Qdrant;

public sealed class QdrantRepository(
    QdrantClient qdrantClient,
    EmbeddingsSettings settings,
    ILogger<QdrantRepository> logger)
    : IWordDefinitionRepository
{
    private readonly QdrantClient _qdrantClient = qdrantClient;
    private readonly EmbeddingsSettings _settings = settings;
    private readonly ILogger<QdrantRepository> _logger = logger;

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

    /// <summary>
    /// Creates the collection if it didn't already exist,
    /// and returns whether the collection existed prior to calling the method.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>true if the collection existed; otherwise false.</returns>
    public async Task<bool> CollectionExists(CancellationToken cancellationToken)
    {
        // during startup, Qdrant may take a moment to load the collection from disk
        return await _collectionExistsStartupPipeline.ExecuteAsync(async cancelToken =>
        {
            try
            {
                var info = await _qdrantClient.GetCollectionInfoAsync(_settings.CollectionName, cancelToken);
                return info.Status == CollectionStatus.Green && info.HasIndexedVectorsCount;
            }
            catch (RpcException rpcException) when (rpcException.StatusCode == StatusCode.NotFound)
            {
                return false;
            }
        }, cancellationToken);
    }

    public async Task CreateCollection(int vectorSize, CancellationToken cancellationToken)
    {
        await _qdrantClient.CreateCollectionAsync(_settings.CollectionName,
            new VectorParams
            {
                Distance = Distance.Cosine,
                Datatype = Datatype.Float32,
                Size = (ulong)vectorSize,
                HnswConfig = new HnswConfigDiff(),
            },
            cancellationToken: cancellationToken);
        await _qdrantClient.CreatePayloadIndexAsync(_settings.CollectionName, nameof(WordDefinition.Word),
            cancellationToken: cancellationToken);
    }

    public async IAsyncEnumerable<WordDefinition> FindClosestMatch(
        ReadOnlyMemory<float> vector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var searchResults = await _qdrantClient.SearchGroupsAsync(_settings.CollectionName, vector,
            groupBy: nameof(WordDefinition.Word),
            groupSize: 20,
            limit: 20,
            scoreThreshold: (float)_settings.ScoreThreshold,
            payloadSelector: true,
            cancellationToken: cancellationToken);

        if (searchResults.Count == 0)
        {
            yield break;
        }

        foreach (var result in searchResults[0].Hits.OrderBy(p => p.Id.Num))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            yield return CreateWordDefinitionFromPayload(result.Payload);
        }
    }

    public async IAsyncEnumerable<WordDefinition> FindMatches(
        ReadOnlyMemory<float> vector,
        int limit = 10,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var searchResults = await _qdrantClient.SearchAsync(_settings.CollectionName, vector,
            limit: (ulong)limit,
            payloadSelector: true,
            cancellationToken: cancellationToken);

        if (searchResults.Count == 0)
        {
            yield break;
        }

        foreach (var result in searchResults)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            yield return CreateWordDefinitionFromPayload(result.Payload);
        }
    }

    public async IAsyncEnumerable<WordDefinition> FindByWord(
        ReadOnlyMemory<float> vector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var searchResults = await _qdrantClient.SearchGroupsAsync(_settings.CollectionName, vector,
            groupBy: nameof(WordDefinition.Word),
            groupSize: 1,
            limit: 10,
            payloadSelector: true,
            cancellationToken: cancellationToken);

        if (searchResults.Count == 0)
        {
            yield break;
        }

        foreach (var result in searchResults)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            yield return CreateWordDefinitionFromPayload(result.Hits[0].Payload);
        }
    }

    private static WordDefinition CreateWordDefinitionFromPayload(MapField<string, Value> payload) =>
        new(
            payload[nameof(WordDefinition.Word)].StringValue,
            payload[nameof(WordDefinition.Definition)].StringValue)
        {
            // TODO: consider storing as json instead
            ReferencedWords = payload[nameof(WordDefinition.ReferencedWords)].ListValue?.Values
                    ?.Select(v => v.StringValue)?.ToArray() ?? []
        };

    public async Task InsertBatch(
        IReadOnlyList<WordDefinitionEmbedding> values,
        CancellationToken cancellationToken = default)
    {
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
