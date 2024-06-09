using Florio.VectorEmbeddings.EmbeddingsModel.ModelData;

using Microsoft.ML;

namespace Florio.VectorEmbeddings.EmbeddingsModel;

public class VectorEmbeddingModel(MLContext mlContext, ITransformer transformer, IDataView dataView)
{
    private readonly MLContext _mlContext = mlContext;
    private readonly ITransformer _transformer = transformer;
    private readonly IDataView _dataView = dataView;
    private readonly PredictionEngine<InputStringData, OutputVectorData> _embeddingGenerator = mlContext.Model.CreatePredictionEngine<InputStringData, OutputVectorData>(transformer);

    public ReadOnlyMemory<float> CalculateVector(string text)
    {
        var output = _embeddingGenerator.Predict(new InputStringData { Text = text });
        return new ReadOnlyMemory<float>(output.Features);
    }

    public void ExportToOnnx(string path)
    {
        using var fileStream = File.Create(path);
        _mlContext.Model.ConvertToOnnx(_transformer, _dataView, fileStream, nameof(OutputVectorData.Features));
    }
}
