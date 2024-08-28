using Florio.Data;
using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Extensions;
using Florio.VectorEmbeddings.Repositories;

namespace Florio.TestApps.VectorSearchComparer;

public class SearchComparerBackgroundService(
    IHostApplicationLifetime hostApplicationLifetime,
    IWordDefinitionParser textParser,
    IVectorEmbeddingModelFactory embeddingsModelFactory,
    [FromKeyedServices("cosmos")] IWordDefinitionRepository cosmosDbRepository,
    [FromKeyedServices("qdrant")] IWordDefinitionRepository qdrantRepository,
    IStringFormatter stringFormatter,
    ILogger<SearchComparerBackgroundService> logger)
    : BackgroundService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime = hostApplicationLifetime;
    private readonly IWordDefinitionParser _textParser = textParser;
    private readonly IVectorEmbeddingModelFactory _embeddingsModelFactory = embeddingsModelFactory;
    private readonly IWordDefinitionRepository _cosmosDbRepository = cosmosDbRepository;
    private readonly IWordDefinitionRepository _qdrantRepository = qdrantRepository;
    private readonly IStringFormatter _stringFormatter = stringFormatter;
    private readonly ILogger<SearchComparerBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var model = _embeddingsModelFactory.GetModel();
        if (model is null)
        {
            throw new FileNotFoundException("Unable to load ONNX model. Ensure the .onnx file exists and the setting points to the correct file.");
        }

        _logger.LogInformation("Checking if vector database has been initialized.");

        var databasesReady = false;

        while (!databasesReady)
        {
            await Task.Delay(TimeSpan.FromSeconds(30));

            databasesReady = await _cosmosDbRepository.CollectionExists(cancellationToken)
                && await _qdrantRepository.CollectionExists(cancellationToken);
        }

        _logger.LogInformation("Vector database have been initialized.");

        await RunKnownTests(model, cancellationToken);
        await RunRandomTests(model, cancellationToken);

        _hostApplicationLifetime.StopApplication();
    }

    private async Task RunKnownTests(VectorEmbeddingModel model, CancellationToken cancellationToken)
    {
        List<string> words =
        [
            "abbellare",
            "Propórre",
            "coll",
            "soprand",
            "Fáre a guísa délla códa",
            "la séra non hà fátto núlla"
        ];

        _logger.LogInformation("Running known test words.");

        foreach (var word in words)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var normalisedWord = _stringFormatter.NormalizeForVector(word);
            var vector = model.CalculateVector(normalisedWord);

            _logger.LogInformation("Search results for {word} (vector: {vector}):", word, vector.ToSparseRepresentation());

            var cosmosResults = await _cosmosDbRepository.FindMatches(vector, cancellationToken: cancellationToken).ToListAsync();
            _logger.LogInformation("CosmosDb:\n{results}", string.Join('\n', cosmosResults.Select(wd => wd.Word)));

            var qdrantResults = await _qdrantRepository.FindMatches(vector, cancellationToken: cancellationToken).ToListAsync();
            _logger.LogInformation("Qdrant:\n{results}", string.Join('\n', qdrantResults.Select(wd => wd.Word)));
        }
    }

    private async Task RunRandomTests(VectorEmbeddingModel model, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running random test words.");

        var words = await _textParser.ParseLines(cancellationToken)
            .GroupBy(wd => _stringFormatter.NormalizeForVector(wd.Word))
            .Select(wg => wg.Key)
            .ToListAsync(cancellationToken: cancellationToken);

        var randomSelection = Random.Shared.GetItems(new ReadOnlySpan<string>([.. words]), 20);

        foreach (var word in randomSelection)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var vector = model.CalculateVector(word);

            _logger.LogInformation("Search results for {word} (vector: {vector}):", word, vector.ToSparseRepresentation());

            var cosmosResults = await _cosmosDbRepository.FindMatches(vector, cancellationToken: cancellationToken).ToListAsync();
            _logger.LogInformation("CosmosDb:\n{results}", string.Join('\n', cosmosResults.Select(wd => wd.Word)));


            var qdrantResults = await _qdrantRepository.FindMatches(vector, cancellationToken: cancellationToken).ToListAsync();
            _logger.LogInformation("Qdrant:\n{results}", string.Join('\n', qdrantResults.Select(wd => wd.Word)));
        }
    }
}
