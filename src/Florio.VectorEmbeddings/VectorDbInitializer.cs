using System.Diagnostics.CodeAnalysis;

using Florio.Data;
using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Repositories;

using Microsoft.Extensions.Logging;

namespace Florio.VectorEmbeddings;
public class VectorDbInitializer(
    IWordDefinitionParser textParser,
    IVectorEmbeddingModelFactory embeddingsModelFactory,
    IWordDefinitionRepository repository,
    IStringFormatter stringFormatter,
    ILogger<VectorDbInitializer> logger)
{
    private readonly IWordDefinitionParser _textParser = textParser;
    private readonly IVectorEmbeddingModelFactory _embeddingsModelFactory = embeddingsModelFactory;
    private readonly IWordDefinitionRepository _repository = repository;
    private readonly IStringFormatter _stringFormatter = stringFormatter;
    private readonly ILogger<VectorDbInitializer> _logger = logger;

    public bool VectorDbInitCompleted { get; set; }

    public async Task<bool> CheckDatabaseInitializedAsync(CancellationToken cancellationToken = default)
    {
        var model = _embeddingsModelFactory.GetModel();
        ThrowIfModelIsNull(model);

        var dbExists = await _repository.CollectionExists(cancellationToken);

        if (dbExists)
        {
            // ensure the vector db actually has data
            var vectorA = model.CalculateVector("a");
            var vectorXistone = model.CalculateVector("xistone");
            var dbHasData =
                await _repository.FindClosestMatch(vectorA, cancellationToken).AnyAsync(cancellationToken)
                && await _repository.FindClosestMatch(vectorXistone, cancellationToken).AnyAsync(cancellationToken);

            if (dbHasData)
            {
                VectorDbInitCompleted = true;
                _logger.LogInformation("Vector Database is already popoulated.");
            }
        }

        return VectorDbInitCompleted;
    }

    public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var model = _embeddingsModelFactory.GetModel();
        ThrowIfModelIsNull(model);

        var wordDefinitions = _textParser.ParseLines(cancellationToken);

        _logger.LogInformation("Populating vector database.");

        var records = await wordDefinitions
            .GroupBy(wd => _stringFormatter.ToPrintableNormalizedString(wd.Word))
            .SelectMany(wg =>
            {
                var key = model.CalculateVector(wg.Key);
                return wg.Select(wd => new WordDefinitionEmbedding(key, wd));
            })
            .ToListAsync(cancellationToken);

        if (!await _repository.CollectionExists(cancellationToken))
        {
            await _repository.CreateCollection(records.First().Vector.Length, cancellationToken);
        }

        try
        {
            await _repository.InsertBatch(records, cancellationToken);

            VectorDbInitCompleted = true;
            _logger.LogInformation("Vector database populated.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to populate vector database.");
        }
    }

    private static void ThrowIfModelIsNull([NotNull] VectorEmbeddingModel? model)
    {
        if (model is null)
        {
            throw new FileNotFoundException("Unable to load ONNX model. Ensure the .onnx file exists and the setting points to the correct file.");
        }
    }
}
