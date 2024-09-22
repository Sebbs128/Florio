using Florio.Data;
using Florio.VectorEmbeddings.EmbeddingsModel;

using Microsoft.Extensions.Logging;

namespace Florio.VectorEmbeddings.Repositories;
public abstract class MigratorBase(
    IWordDefinitionParser textParser,
    IVectorEmbeddingModelFactory embeddingsModelFactory,
    IStringFormatter stringFormatter,
    ILogger logger)
    : IRepositoryMigrator
{
    protected readonly IWordDefinitionParser _textParser = textParser;
    protected readonly IVectorEmbeddingModelFactory _embeddingsModelFactory = embeddingsModelFactory;
    protected readonly IStringFormatter _stringFormatter = stringFormatter;
    protected readonly ILogger _logger = logger;

    public abstract Task MigrateAsync(CancellationToken cancellationToken = default);
    protected abstract Task CreateCollection(int vectorSize, string collectionName, CancellationToken cancellationToken = default);

    protected abstract Task InsertRecords(string collectionName, IReadOnlyList<WordDefinitionEmbedding> records, CancellationToken cancellationToken = default);

    protected virtual async Task ReseedCollection(string collectionName, VectorEmbeddingModel model, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Populating vector database.");

        var records = await RetrieveAndParsePayload(model, cancellationToken);

        try
        {
            await InsertRecords(collectionName, records, cancellationToken);
            _logger.LogInformation("Vector database populated.");
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Failed to populate vector database.");
        }
    }

    protected virtual async Task<IReadOnlyList<WordDefinitionEmbedding>> RetrieveAndParsePayload(VectorEmbeddingModel model, CancellationToken cancellationToken = default)
    {
        var wordDefinitions = _textParser.ParseLines(cancellationToken);

        return await wordDefinitions
            .GroupBy(wd => _stringFormatter.NormalizeForVector(wd.Word))
            .SelectMany(wg =>
            {
                var key = model.CalculateVector(wg.Key);
                return wg.Select(wd => new WordDefinitionEmbedding(key, wd));
            })
            .ToListAsync(cancellationToken);
    }
}
