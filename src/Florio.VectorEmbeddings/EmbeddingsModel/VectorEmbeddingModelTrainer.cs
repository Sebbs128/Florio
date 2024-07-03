using System.Data;

using Florio.VectorEmbeddings.EmbeddingsModel.ModelData;

using Microsoft.ML;
using Microsoft.ML.Transforms.Text;

namespace Florio.VectorEmbeddings.EmbeddingsModel;
public class VectorEmbeddingModelTrainer(MLContext mlContext)
{
    private readonly MLContext _mlContext = mlContext;

    public void TrainAndSaveModel(IEnumerable<string> data, string onnxModelPath)
    {
        if (File.Exists(onnxModelPath))
        {
            File.Delete(onnxModelPath);
        }

        Console.WriteLine("Training vector embedding model");

        var dataView = _mlContext.Data.LoadFromEnumerable(data
            .Select(s => new InputStringData { Text = s }));

        var embeddingPipeline =
            _mlContext.Transforms.Text.TokenizeIntoCharactersAsKeys("CharTokens", "Text", useMarkerCharacters: true)
            .Append(_mlContext.Transforms.Text.ProduceNgrams("Features", "CharTokens",
                ngramLength: 3,
                useAllLengths: false,
                weighting: NgramExtractingEstimator.WeightingCriteria.Tf));

        var transformer = embeddingPipeline.Fit(dataView);

        Console.WriteLine("Saving vector embedding model to .onnx file.");

        using var fileStream = File.Create(onnxModelPath);
        _mlContext.Model.ConvertToOnnx(transformer, dataView, fileStream, nameof(OutputVectorData.Features));

    }
}
