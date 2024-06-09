using Florio.VectorEmbeddings;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Florio.WebApp.HealthChecks;

public class VectorDatabaseHealthCheck(QdrantClient qdrantClient, EmbeddingsSettings settings) : IHealthCheck
{
    private readonly QdrantClient _qdrantClient = qdrantClient;
    private readonly EmbeddingsSettings _settings = settings;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return await _qdrantClient.GetCollectionInfoAsync(_settings.CollectionName, cancellationToken) switch
        {
            { Status: CollectionStatus.Green } => HealthCheckResult.Healthy("Vector Database ready."),
            _ => HealthCheckResult.Unhealthy("Vector Database not ready.")
        };
    }
}
