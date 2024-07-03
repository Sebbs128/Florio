using Florio.VectorEmbeddings.EmbeddingsModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;

namespace Florio.VectorEmbeddings.Extensions;
public static class PredictionEnginePoolBuilderExtensions
{
    public static PredictionEnginePoolBuilder<TData, TPrediction> FromOnnxFile<TData, TPrediction>(
        this PredictionEnginePoolBuilder<TData, TPrediction> builder, string filePath)
        where TData : class
        where TPrediction : class, new()
    {
        return builder.FromOnnxFile(string.Empty, filePath, true);
    }

    public static PredictionEnginePoolBuilder<TData, TPrediction> FromOnnxFile<TData, TPrediction>(
    this PredictionEnginePoolBuilder<TData, TPrediction> builder, string filePath, bool watchForChanges)
    where TData : class
    where TPrediction : class, new()
    {
        return builder.FromOnnxFile(string.Empty, filePath, watchForChanges);
    }
    public static PredictionEnginePoolBuilder<TData, TPrediction> FromOnnxFile<TData, TPrediction>(
        this PredictionEnginePoolBuilder<TData, TPrediction> builder, string modelName, string filePath)
        where TData : class
        where TPrediction : class, new()
    {
        return builder.FromOnnxFile(modelName, filePath, true);
    }

    public static PredictionEnginePoolBuilder<TData, TPrediction> FromOnnxFile<TData, TPrediction>(
        this PredictionEnginePoolBuilder<TData, TPrediction> builder, string modelName, string filePath, bool watchForChanges)
        where TData : class
        where TPrediction : class, new()
    {
        builder.Services.AddTransient<OnnxModelLoader<TData>>();
        builder.Services.AddOptions<PredictionEnginePoolOptions<TData, TPrediction>>(modelName)
            .Configure<OnnxModelLoader<TData>>((options, loader) =>
            {
                loader.Start(filePath, watchForChanges);
                options.ModelLoader = loader;
            });
        return builder;
    }
}
