using Florio.VectorEmbeddings.EmbeddingsModel.ModelData;

using Microsoft.Extensions.ML;

namespace Florio.VectorEmbeddings.EmbeddingsModel;

public class VectorEmbeddingModel
{
    private readonly PredictionEnginePool<InputStringData, OutputVectorData> _predictionEnginePool;

    internal VectorEmbeddingModel(PredictionEnginePool<InputStringData, OutputVectorData> predictionEnginePool)
    {
        _predictionEnginePool = predictionEnginePool;
    }

    public ReadOnlyMemory<float> CalculateVector(string text)
    {
        var output = _predictionEnginePool.Predict(new InputStringData { Text = text });
        return new ReadOnlyMemory<float>(output.Features);
    }
}
