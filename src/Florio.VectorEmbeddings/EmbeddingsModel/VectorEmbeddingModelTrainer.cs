using System.Data;
using System.Diagnostics;
using System.Text.Json;

using Florio.VectorEmbeddings.EmbeddingsModel.ModelData;
using Florio.VectorEmbeddings.Extensions;

using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;

namespace Florio.VectorEmbeddings.EmbeddingsModel;
public class VectorEmbeddingModelTrainer(MLContext mlContext, IOptions<EmbeddingsSettings> options)
{
    private readonly MLContext _mlContext = mlContext;
    private readonly IOptions<EmbeddingsSettings> _options = options;

    public async Task TrainAndSaveModel(IEnumerable<string> data, string onnxModelPath)
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
                ngramLength: 2,
                useAllLengths: true,
                weighting: NgramExtractingEstimator.WeightingCriteria.Tf));

        var transformer = embeddingPipeline.Fit(dataView);

        var predictionEngine = _mlContext.Model.CreatePredictionEngine<InputStringData, OutputVectorData>(transformer);

#if DEBUG
        // test a sample selection. This also helps as a quick catch if entries might be empty or all 0s
        var samples = Random.Shared.GetItems(new ReadOnlySpan<string>(data.ToArray()), 25);

        foreach (var item in samples)
        {
            var prediction = predictionEngine.Predict(new InputStringData { Text = item });
            Debug.Assert(prediction.Features.Any(f => f > 0));
            Debug.WriteLine($"'{item}': {new ReadOnlyMemory<float>(prediction.Features).ToSparseRepresentation()}");
        }
#endif

        Console.WriteLine("Saving vector embedding model to .onnx file.");

        using var onnxFileStream = File.Create(onnxModelPath);
        _mlContext.Model.ConvertToOnnx(transformer, dataView, onnxFileStream, nameof(OutputVectorData.Features));

        using var infoFileStream = File.Create(Path.ChangeExtension(onnxModelPath, ".json"));
        await JsonSerializer.SerializeAsync(infoFileStream, new
        {
            NumberOfVectors = data.Count(),
            VectorSize = (predictionEngine.OutputSchema.Last().Type as VectorDataViewType).Size
        });
    }
}
