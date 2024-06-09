using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Repositories;
using Florio.WebApp.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;

namespace Florio.VectorEmbeddings.Extensions;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVectorEmbeddings<TWordDefinitionRepository>(
        this IServiceCollection services)
        where TWordDefinitionRepository : class, IWordDefinitionRepository
    {
        return services
            .AddSingleton<MLContext>()
            .AddSingleton<VectorEmbeddingModelFactory>()
            .AddSingleton<IVectorEmbeddingModelFactory>(sp =>
                sp.GetRequiredService<VectorEmbeddingModelFactory>())

            .AddSingleton<IWordDefinitionRepository, TWordDefinitionRepository>()
            .AddSingleton(sp => sp.GetRequiredService<IConfiguration>()
                .GetSection(nameof(EmbeddingsSettings))
                .Get<EmbeddingsSettings>() ?? new EmbeddingsSettings())

            .AddHostedService<EmbeddingsInitializerService>();
    }
}
