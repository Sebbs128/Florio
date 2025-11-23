using Florio.Data;
using Florio.VectorEmbeddings.EmbeddingsModel;

using Microsoft.Extensions.Logging;

namespace Florio.VectorEmbeddings.Repositories;
public abstract partial class MigratorBase(
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
        Log.PopulatingVectorDatabase(_logger);

        var records = await RetrieveAndParsePayload(model, cancellationToken);

        try
        {
            await InsertRecords(collectionName, records, cancellationToken);
            Log.VectorDatabasePopulated(_logger);
        }
        catch (Exception ex)
        {
            Log.FailedToPopulateVectorDatabase(_logger, ex);
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

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Information, "Populating vector database.")]
        public static partial void PopulatingVectorDatabase(ILogger logger);

        [LoggerMessage(LogLevel.Information, "Vector database populated.")]
        public static partial void VectorDatabasePopulated(ILogger logger);

        [LoggerMessage(LogLevel.Error, "Failed to populate vector database.")]
        public static partial void FailedToPopulateVectorDatabase(ILogger logger, Exception exception);

    }
}
