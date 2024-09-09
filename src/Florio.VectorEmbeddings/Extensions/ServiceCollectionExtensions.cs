using Florio.VectorEmbeddings;
using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.EmbeddingsModel.ModelData;
using Florio.VectorEmbeddings.Extensions;
using Florio.VectorEmbeddings.Repositories;
using Florio.VectorEmbeddings.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.ML;
using Microsoft.ML;

namespace Microsoft.Extensions.DependencyInjection;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVectorEmbeddingsTrainer(this IServiceCollection services)
    {
        return services
            .AddSingleton<MLContext>()
            .AddSingleton<VectorEmbeddingModelTrainer>();
    }

    public static IServiceCollection AddVectorEmbeddingsModel(
        this IServiceCollection services, string embeddingVectorModelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingVectorModelPath, nameof(embeddingVectorModelPath));

        services
            .AddSingleton<IVectorEmbeddingModelFactory, VectorEmbeddingModelFactory>()
            .AddPredictionEnginePool<InputStringData, OutputVectorData>()
            .FromOnnxFile(embeddingVectorModelPath, false);

        return services;
    }

    public static IServiceCollection AddVectorEmbeddingsRepository<TWordDefinitionRepository>(this IServiceCollection services)
        where TWordDefinitionRepository : class, IWordDefinitionRepository
    {
        return services
            .AddSingleton<IWordDefinitionRepository, TWordDefinitionRepository>()
            .AddSingleton(sp => sp.GetRequiredService<IConfiguration>()
                .GetSection(nameof(EmbeddingsSettings))
                .Get<EmbeddingsSettings>() ?? new EmbeddingsSettings());
    }

    public static IServiceCollection AddVectorEmbeddingsMigrations<TMigrator>(this IServiceCollection services)
        where TMigrator : class, IRepositoryMigrator
    {
        services
            .AddSingleton<IRepositoryMigrator, TMigrator>()
            .AddSingleton(sp => sp.GetRequiredService<IConfiguration>()
                .GetSection(nameof(VectorDbInitializerSettings))
                .Get<VectorDbInitializerSettings>() ?? new VectorDbInitializerSettings());
        return services;
    }
}
