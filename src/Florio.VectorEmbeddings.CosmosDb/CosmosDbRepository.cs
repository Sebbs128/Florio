﻿using System.Net;
using System.Runtime.CompilerServices;

using Florio.Data;
using Florio.VectorEmbeddings.CosmosDb.Models;
using Florio.VectorEmbeddings.Extensions;
using Florio.VectorEmbeddings.Repositories;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

using Polly;

namespace Florio.VectorEmbeddings.CosmosDb;

public sealed class CosmosDbRepository(
    CosmosClient cosmosClient,
    EmbeddingsSettings settings,
    ILogger<CosmosDbRepository> logger)
    : IWordDefinitionRepository
{
    private readonly CosmosClient _cosmosClient = cosmosClient;
    private readonly EmbeddingsSettings _settings = settings;
    private readonly ILogger<CosmosDbRepository> _logger = logger;

    private static readonly ResiliencePipeline _collectionExistsStartupPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new Polly.Retry.RetryStrategyOptions
        {
            Delay = TimeSpan.FromSeconds(10),
            ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(),
            MaxRetryAttempts = 10
        })
        .Build();

    private const string PartitionKeyPath = "/partitionKey";

    private Container GetContainer(CosmosClient client) => client.GetContainer(_settings.CollectionName, _settings.CollectionName);

    public async Task<bool> CollectionExists(CancellationToken cancellationToken = default)
    {
        return await _collectionExistsStartupPipeline.ExecuteAsync(async cancelToken =>
        {
            try
            {
                var containerResponse = await GetContainer(_cosmosClient)
                    .ReadContainerAsync(cancellationToken: cancellationToken);

                if (containerResponse is not { StatusCode: HttpStatusCode.Created or HttpStatusCode.OK })
                {
                    return false;
                }

                var container = containerResponse.Container;

                return container.GetItemLinqQueryable<VectorGroupDocument>().Count() > 73_000;
            }
            catch (CosmosException cosmosEx) when (cosmosEx is { StatusCode: HttpStatusCode.NotFound })
            {
                return false;
            }
        }, cancellationToken);
    }

    public async IAsyncEnumerable<WordDefinition> FindClosestMatch(
        ReadOnlyMemory<float> vector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string queryText = """
            SELECT TOP 1 VectorDistance(v.vector, @targetVector) AS score, v.wordDefinitions
            FROM vectorGroupDocument v
            WHERE VectorDistance(v.vector, @targetVector) > @threshold
            ORDER BY VectorDistance(v.vector, @targetVector)
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@targetVector", vector.ToArray())
            .WithParameter("@threshold", _settings.ScoreThreshold);

        using var feed = GetContainer(_cosmosClient)
            .GetItemQueryIterator<QueryResult>(query);

        while (feed.HasMoreResults && !cancellationToken.IsCancellationRequested)
        {
            var response = await feed.ReadNextAsync(cancellationToken);
            foreach (var result in response)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Nearest result(s) to {vector} is '{result}' with similarity {similarity}",
                        vector.ToSparseRepresentation(),
                        result.wordDefinitions.First().word,
                        result.score);
                }

                foreach (var item in result.wordDefinitions)
                {
                    yield return item;
                }
            }
        }
    }

    public async IAsyncEnumerable<WordDefinition> FindMatches(
        ReadOnlyMemory<float> vector,
        int limit = 10,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string queryText = """
            SELECT VectorDistance(v.vector, @targetVector) AS score, v.wordDefinitions
            FROM vectorGroupDocument v
            ORDER BY VectorDistance(v.vector, @targetVector)
            OFFSET 0 LIMIT @limit
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@targetVector", vector.ToArray())
            .WithParameter("@limit", limit);

        using var feed = GetContainer(_cosmosClient)
            .GetItemQueryIterator<QueryResult>(query);

        while (feed.HasMoreResults && !cancellationToken.IsCancellationRequested)
        {
            var response = await feed.ReadNextAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Search for {vector} found {count} results", vector.ToSparseRepresentation(), response.Count());
            }
            foreach (var result in response)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Result \"{word}\" has similarity score {score}", result.wordDefinitions[0].word, result.score);
                }

                foreach (var item in result.wordDefinitions)
                {
                    yield return item;
                }
            }
        }
    }

    public async IAsyncEnumerable<WordDefinition> FindByWord(
        ReadOnlyMemory<float> vector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string queryText = """
            SELECT TOP 10 VectorDistance(v.vector, @targetVector) AS score, v.wordDefinitions
            FROM vectorGroupDocument v
            ORDER BY VectorDistance(v.vector, @targetVector)
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@targetVector", vector.ToArray());

        using var feed = GetContainer(_cosmosClient)
            .GetItemQueryIterator<QueryResult>(query);

        while (feed.HasMoreResults && !cancellationToken.IsCancellationRequested)
        {
            var response = await feed.ReadNextAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Search for {vector} found {count} results", vector.ToSparseRepresentation(), response.Count());
            }
            foreach (var result in response)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Result \"{word}\" has similarity score {score}", result.wordDefinitions[0].word, result.score);
                }

                yield return result.wordDefinitions.First();
            }
        }
    }
}
