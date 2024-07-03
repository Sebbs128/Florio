using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Repositories;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Florio.WebApp.HealthChecks;

public class VectorDatabaseHealthCheck(
    IWordDefinitionRepository repository,
    IVectorEmbeddingModelFactory embeddingsModelFactory) : IHealthCheck
{
    private readonly IWordDefinitionRepository _repository = repository;
    private readonly IVectorEmbeddingModelFactory _embeddingsModelFactory = embeddingsModelFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (await _repository.CollectionExists(cancellationToken))
        {
            var model = _embeddingsModelFactory.GetModel();
            var vectorA = model!.CalculateVector("a");
            var vectorXistone = model!.CalculateVector("xistone");

            var hasData = await _repository.FindClosestMatch(vectorA, cancellationToken).AnyAsync(cancellationToken)
                && await _repository.FindClosestMatch(vectorXistone, cancellationToken).AnyAsync(cancellationToken);

            if (hasData)
            {
                return HealthCheckResult.Healthy("Vector Database ready.");
            }
        }

        return HealthCheckResult.Degraded("Vector Database not ready.");
    }
}
