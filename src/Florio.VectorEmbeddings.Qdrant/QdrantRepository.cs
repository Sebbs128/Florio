using System.Diagnostics;
using System.Runtime.CompilerServices;

using Florio.Data;
using Florio.VectorEmbeddings.Repositories;

using Google.Protobuf.Collections;

using Grpc.Core;

using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Florio.VectorEmbeddings.Qdrant;

public class QdrantRepository(QdrantClient qdrantClient, EmbeddingsSettings settings) : IWordDefinitionRepository
{
    private readonly QdrantClient _qdrantClient = qdrantClient;
    private readonly EmbeddingsSettings _settings = settings;

    private static readonly TimeSpan _delay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Creates the collection if it didn't already exist,
    /// and returns whether the collection existed prior to calling the method.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>true if the collection existed; otherwise false.</returns>
    public async Task<bool> CollectionExists(CancellationToken cancellationToken)
    {
        // during startup, Qdrant may take a moment to load the collection from disk
        for (int i = 0; i < 10; i++)
        {
            try
            {
                if (await _qdrantClient.CollectionExistsAsync(_settings.CollectionName, cancellationToken))
                {
                    // ensure it has some data (eg. in case someone deleted all data in the collection to force a reload)
                    var info = await _qdrantClient.GetCollectionInfoAsync(_settings.CollectionName, cancellationToken);
                    return info.Status == CollectionStatus.Green && info.HasIndexedVectorsCount;
                }
                return false;
            }
            catch (Grpc.Core.RpcException rpcException) when (rpcException.StatusCode == StatusCode.Unavailable)
            {
                await Task.Delay(_delay, cancellationToken);
            }
        }
        return false;
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
        int limit = 20,
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
        const int batchSize = 5_000;

        var records = values
            .Select((v, i) => new PointStruct
            {
                Id = (ulong)i,
                Vectors = v.Vector.ToArray(),
                Payload =
                {
                    [nameof(WordDefinition.Word)] = v.WordDefinition.Word,
                    [nameof(WordDefinition.Definition)] = v.WordDefinition.Definition,
                    [nameof(WordDefinition.ReferencedWords)] = v.WordDefinition.ReferencedWords ?? []
                }
            })
            .ToList();

        for (int i = 0; i < records.Count; i += batchSize)
        {
            var updateResult = await _qdrantClient.UpsertAsync(_settings.CollectionName,
                records.Skip(i).Take(batchSize).ToList(),
                wait: true,
                cancellationToken: cancellationToken);
            Debug.Assert(updateResult.Status == UpdateStatus.Completed);
            await Task.Delay(1000, cancellationToken);
        }
    }

    public IAsyncEnumerable<WordDefinition> FindByWord(string search, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
