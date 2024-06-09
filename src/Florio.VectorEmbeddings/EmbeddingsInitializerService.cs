using System.Diagnostics.CodeAnalysis;

using Florio.Data;
using Florio.VectorEmbeddings;
using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Repositories;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Florio.WebApp.Services;

internal class EmbeddingsInitializerService(
    IWordDefinitionParser textParser,
    VectorEmbeddingModelFactory embeddingsModelFactory,
    IWordDefinitionRepository repository,
    IStringFormatter stringFormatter,
    EmbeddingsSettings settings,
    ILogger<EmbeddingsInitializerService> logger)
    : IHostedLifecycleService
{
    private readonly IWordDefinitionParser _textParser = textParser;
    private readonly VectorEmbeddingModelFactory _embeddingsModelFactory = embeddingsModelFactory;
    private readonly IWordDefinitionRepository _repository = repository;
    private readonly IStringFormatter _stringFormatter = stringFormatter;
    private readonly EmbeddingsSettings _settings = settings;
    private readonly ILogger<EmbeddingsInitializerService> _logger = logger;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        var dbExists = await _repository.CollectionExists(cancellationToken);
        var modelExists = TryLoadFromOnnx(_settings.OnnxFilePath, out VectorEmbeddingModel? model);
        var dbHasData = false;

        if (dbExists && modelExists)
        {
            // ensure the vector db actually has data
            var vector = model!.CalculateVector("a");
            dbHasData = await _repository.FindClosestMatch(vector, cancellationToken).AnyAsync();
            if (dbHasData)
            {
                _logger.LogInformation("Vector Database and Embeddings Model both exist.");
                return;
            }
        }

        var wordDefinitions = await _textParser.ParseLines(cancellationToken).ToListAsync(cancellationToken);

        if (!modelExists)
        {
            _logger.LogInformation("Generating new embeddings model.");

            IEnumerable<string> data = wordDefinitions.Select(wd =>
                _stringFormatter.ToPrintableNormalizedString(wd.Word));
            model = _embeddingsModelFactory.CreateFromData(data);

            _logger.LogInformation("Embeddings model generated.");

            _logger.LogInformation("Saving embeddings model as .onnx file to {path}.", _settings.OnnxFilePath);
            model.ExportToOnnx(_settings.OnnxFilePath);
        }

        if (!dbExists || !dbHasData)
        {
            _logger.LogInformation("Populating vector database.");

            var records = wordDefinitions
                .Select(wd => new WordDefinitionEmbedding(
                    model!.CalculateVector(_stringFormatter.ToPrintableNormalizedString(wd.Word)),
                    wd))
                .ToList();

            if (!dbExists)
            {
                await _repository.CreateCollection(records.First().Vector.Length, cancellationToken);
            }

            await _repository.InsertBatch(records, cancellationToken);

            _logger.LogInformation("Vector database populated.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool TryLoadFromOnnx(string onnxFilePath, [NotNullWhen(true)] out VectorEmbeddingModel? embeddingsModel)
    {
        embeddingsModel = null;
        try
        {
            embeddingsModel = _embeddingsModelFactory.CreateFromOnnxFile(onnxFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't load .onnx file from {path}. Initializer will fall back to loading from parser.", onnxFilePath);
        }

        return embeddingsModel is not null;
    }
}
