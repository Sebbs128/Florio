using System.Runtime.CompilerServices;
using System.Text.Json;

using Florio.Data;
using Florio.VectorEmbeddings.Repositories;

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Memory;

namespace Florio.VectorEmbeddings.SKInMemory;

#pragma warning disable SKEXP0001, SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public class SemanticKernelMemoryRepository(
    EmbeddingsSettings settings,
    ILogger<SemanticKernelMemoryRepository> logger) : IWordDefinitionRepository
{
    private readonly VolatileMemoryStore _vectorStore = new();
    private readonly EmbeddingsSettings _settings = settings;
    private readonly ILogger<SemanticKernelMemoryRepository> _logger = logger;

    public Task<bool> CollectionExists(CancellationToken cancellationToken = default)
    {
        return _vectorStore.DoesCollectionExistAsync(_settings.CollectionName, cancellationToken);
    }

    public async Task CreateCollection(int vectorSize, CancellationToken cancellationToken = default)
    {
        await _vectorStore.CreateCollectionAsync(_settings.CollectionName, cancellationToken);
    }

    public async IAsyncEnumerable<WordDefinition> FindClosestMatch(
        ReadOnlyMemory<float> vector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var searchResult = await _vectorStore
            .GetNearestMatchesAsync(_settings.CollectionName, vector, limit: 20,
                minRelevanceScore: _settings.ScoreThreshold,
                cancellationToken: cancellationToken)
            .OrderByDescending(r => r.Item2)
            .GroupBy(r => r.Item1.Metadata.Text)
            .FirstOrDefaultAsync(cancellationToken);

        if (searchResult is null || !await searchResult.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("No result for {vector} found within {threshold} distance", vector, _settings.ScoreThreshold);
            yield break;
        }

        await foreach (var result in searchResult)
        {
            _logger.LogInformation("Nearest result to {vector} has similarity {similarity}", vector, result.Item2);
            yield return CreateWordDefinition(result.Item1);
        }
    }

    public IAsyncEnumerable<WordDefinition> FindMatches(
        ReadOnlyMemory<float> vector,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        return _vectorStore.GetNearestMatchesAsync(_settings.CollectionName, vector, limit,
            minRelevanceScore: _settings.ScoreThreshold,
            cancellationToken: cancellationToken)
            .Select((result) => CreateWordDefinition(result.Item1));
    }

    public IAsyncEnumerable<WordDefinition> FindByWord(
        ReadOnlyMemory<float> vector,
        CancellationToken cancellationToken = default)
    {
        return _vectorStore.GetNearestMatchesAsync(_settings.CollectionName, vector,
            limit: 20,
            cancellationToken: cancellationToken)
            .Select((result) => CreateWordDefinition(result.Item1))
            .GroupBy(wd => wd.Word)
            .SelectAwait(async g => await g.FirstAsync());
    }

    public async Task InsertBatch(
        IReadOnlyList<WordDefinitionEmbedding> values,
        CancellationToken cancellationToken = default)
    {
        var records = values
            .Select(v => MemoryRecord.LocalRecord(
                id: Guid.NewGuid().ToString(),
                embedding: v.Vector,
                text: v.WordDefinition.Word,
                description: v.WordDefinition.Definition,
                additionalMetadata: JsonSerializer.Serialize(v.WordDefinition.ReferencedWords ?? [])));
        await _vectorStore.UpsertBatchAsync(_settings.CollectionName, records, cancellationToken).CountAsync(cancellationToken);
    }

    private static WordDefinition CreateWordDefinition(MemoryRecord memoryRecord)
    {
        return new WordDefinition(
            memoryRecord.Metadata.Text,
            memoryRecord.Metadata.Description)
        {
            ReferencedWords = JsonSerializer.Deserialize<string[]>(memoryRecord.Metadata.AdditionalMetadata)
        };
    }
}
#pragma warning restore SKEXP0001, SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
