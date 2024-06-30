using Florio.Data;
using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Repositories;

using Microsoft.Extensions.Hosting;

namespace Florio.Utilities.VectorTesting;
internal class TestEmbeddingsService(
    IWordDefinitionRepository repository,
    IVectorEmbeddingModelFactory vectorEmbeddingModelFactory,
    IStringFormatter stringFormatter,
    IHostApplicationLifetime hostLifetime) : IHostedService
{
    private readonly IWordDefinitionRepository _repository = repository;
    private readonly IVectorEmbeddingModelFactory _vectorEmbeddingModelFactory = vectorEmbeddingModelFactory;
    private readonly IStringFormatter _stringFormatter = stringFormatter;
    private readonly IHostApplicationLifetime _hostLifetime = hostLifetime;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var model = _vectorEmbeddingModelFactory.GetModel();

        string[] tests =
        [
            "a",
            "abbellare",
            "Propórre",
            "Cóllerra", // Cóllera
        ];

        Console.WriteLine("Top 10 matches for \"abbellare\":");
        await foreach (var match in _repository.FindMatches(model.CalculateVector("abbellare"), 10, cancellationToken: cancellationToken))
        {
            Console.WriteLine($"{match.Word}: {match.Definition}");
            Console.WriteLine();
        }

        foreach (var testWord in tests)
        {
            var normalisedWord = _stringFormatter.ToPrintableNormalizedString(testWord);
            var results = _repository.FindClosestMatch(model.CalculateVector(normalisedWord), cancellationToken);

            if (await results.AnyAsync())
            {
                var bestMatch = await results.FirstAsync();
                Console.WriteLine($"Best match for \"{testWord}\" was \"{bestMatch.Word}\"");
                await foreach (var match in results)
                {
                    Console.WriteLine($"{match.Word}: {match.Definition}");
                }
            }
            else
            {
                Console.WriteLine($"No best match for \"{testWord}\" was found.");
            }
            Console.WriteLine();
        }

        _hostLifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
