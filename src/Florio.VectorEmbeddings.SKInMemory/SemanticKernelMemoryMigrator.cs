using System.Text.Json;

using Florio.Data;
using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Repositories;

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Memory;

namespace Florio.VectorEmbeddings.SKInMemory;

#pragma warning disable SKEXP0001, SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public sealed class SemanticKernelMemoryMigrator(
    VolatileMemoryStore vectorStore,
    IWordDefinitionParser textParser,
    IVectorEmbeddingModelFactory embeddingsModelFactory,
    IStringFormatter stringFormatter,
    EmbeddingsSettings settings,
    ILogger<SemanticKernelMemoryMigrator> logger)
    : MigratorBase(textParser, embeddingsModelFactory, stringFormatter, logger)
{
    private readonly VolatileMemoryStore _vectorStore = vectorStore;
    private readonly EmbeddingsSettings _settings = settings;

    public override async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var model = _embeddingsModelFactory.GetModel();
        await CreateCollection(0, _settings.CollectionName, cancellationToken);

        await ReseedCollection(_settings.CollectionName, model, cancellationToken);
    }

    protected override async Task CreateCollection(int vectorSeize, string collectionName, CancellationToken cancellationToken = default)
    {
        await _vectorStore.CreateCollectionAsync(_settings.CollectionName, cancellationToken);
    }

    protected override async Task InsertRecords(string collectionName, IReadOnlyList<WordDefinitionEmbedding> records, CancellationToken cancellationToken = default)
    {
        var toAdd = records
            .Select(v => MemoryRecord.LocalRecord(
                id: Guid.NewGuid().ToString(),
                embedding: v.Vector,
                text: v.WordDefinition.Word,
                description: v.WordDefinition.Definition,
                additionalMetadata: JsonSerializer.Serialize(v.WordDefinition.ReferencedWords ?? [])));

        await _vectorStore.UpsertBatchAsync(_settings.CollectionName, toAdd, cancellationToken).CountAsync(cancellationToken);
    }
}
#pragma warning restore SKEXP0001, SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
