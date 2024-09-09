using System.Buffers;
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
}
