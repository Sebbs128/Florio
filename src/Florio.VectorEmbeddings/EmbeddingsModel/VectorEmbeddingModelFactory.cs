using Florio.VectorEmbeddings.EmbeddingsModel.ModelData;

using Microsoft.Extensions.ML;

namespace Florio.VectorEmbeddings.EmbeddingsModel;

internal class VectorEmbeddingModelFactory(PredictionEnginePool<InputStringData, OutputVectorData> predictionEnginePool) : IVectorEmbeddingModelFactory
{
    private readonly PredictionEnginePool<InputStringData, OutputVectorData> _predictionEnginePool = predictionEnginePool;

    public VectorEmbeddingModel GetModel() => new(_predictionEnginePool);
}
