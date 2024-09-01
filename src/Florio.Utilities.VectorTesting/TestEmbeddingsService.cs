﻿using Florio.Data;
using Florio.VectorEmbeddings;
using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Repositories;

using Microsoft.Extensions.Hosting;

namespace Florio.Utilities.VectorTesting;
internal class TestEmbeddingsService(
    VectorDbInitializer vectorDbInitializer,
    IWordDefinitionRepository repository,
    IVectorEmbeddingModelFactory vectorEmbeddingModelFactory,
    IStringFormatter stringFormatter,
    IHostApplicationLifetime hostLifetime)
    : BackgroundService
{
    private readonly VectorDbInitializer _vectorDbInitializer = vectorDbInitializer;
    private readonly IWordDefinitionRepository _repository = repository;
    private readonly IVectorEmbeddingModelFactory _vectorEmbeddingModelFactory = vectorEmbeddingModelFactory;
    private readonly IStringFormatter _stringFormatter = stringFormatter;
    private readonly IHostApplicationLifetime _hostLifetime = hostLifetime;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var model = _vectorEmbeddingModelFactory.GetModel();

        // wait for vector db initialization
        while (!_vectorDbInitializer.VectorDbInitCompleted)
        {
            await Task.Delay(10, cancellationToken);
        }

        string[] tests =
        [
            "a",
            "abbellare",
            "Propórre",
            "Cóllerra", // Cóllera
        ];

        foreach (var testWord in tests)
        {
            var normalisedWord = _stringFormatter.NormalizeForVector(testWord);
            Console.WriteLine($"Top 10 matches for \"{testWord}\":"); // equivalent to page route /Search?term={word}
            await foreach (var match in _repository.FindMatches(model.CalculateVector(normalisedWord), 10, cancellationToken: cancellationToken))
            {
                Console.WriteLine($"{match.Word}: {match.Definition}");
                Console.WriteLine();
            }

            Console.WriteLine($"Direct lookup for \"{testWord}\":"); // equivalent to page route /Italian/{word}
            var results = _repository.FindClosestMatch(model.CalculateVector(normalisedWord), cancellationToken);

            if (await results.AnyAsync(cancellationToken))
            {
                var bestMatch = await results.FirstAsync(cancellationToken);
                Console.WriteLine($"Best match for \"{testWord}\" was \"{bestMatch.Word}\"");
                await foreach (var match in results)
                {
                    Console.WriteLine($"{match.Word}: {match.Definition}");
                }
            }
            else
            {
                Console.WriteLine($"No best match for \"{testWord}\" was found within the score threshold.");
            }
            Console.WriteLine();
        }

        _hostLifetime.StopApplication();
    }
}
