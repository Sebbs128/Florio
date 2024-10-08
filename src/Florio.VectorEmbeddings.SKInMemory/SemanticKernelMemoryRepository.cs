﻿using System.Runtime.CompilerServices;
using System.Text.Json;

using Florio.Data;
using Florio.VectorEmbeddings.Extensions;
using Florio.VectorEmbeddings.Repositories;

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Memory;

namespace Florio.VectorEmbeddings.SKInMemory;

#pragma warning disable SKEXP0001, SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public class SemanticKernelMemoryRepository(
    VolatileMemoryStore vectorStore,
    EmbeddingsSettings settings,
    ILogger<SemanticKernelMemoryRepository> logger) : IWordDefinitionRepository
{
    private readonly VolatileMemoryStore _vectorStore = vectorStore;
    private readonly EmbeddingsSettings _settings = settings;
    private readonly ILogger<SemanticKernelMemoryRepository> _logger = logger;

    public Task<bool> CollectionExists(CancellationToken cancellationToken = default)
    {
        return _vectorStore.DoesCollectionExistAsync(_settings.CollectionName, cancellationToken);
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
            _logger.LogDebug("No result for {vector} found within {threshold} distance", vector.ToSparseRepresentation(), _settings.ScoreThreshold);
            yield break;
        }

        await foreach (var result in searchResult)
        {
            _logger.LogDebug("Nearest result to {vector} has similarity {similarity}", vector.ToSparseRepresentation(), result.Item2);
            yield return CreateWordDefinition(result.Item1);
        }
    }

    public IAsyncEnumerable<WordDefinition> FindMatches(
        ReadOnlyMemory<float> vector,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        return _vectorStore.GetNearestMatchesAsync(_settings.CollectionName, vector, limit,
            //minRelevanceScore: _settings.ScoreThreshold,
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
