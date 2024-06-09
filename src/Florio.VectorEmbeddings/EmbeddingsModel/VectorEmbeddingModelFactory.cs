using System.Data;
using System.Diagnostics.CodeAnalysis;

using Florio.VectorEmbeddings.EmbeddingsModel.ModelData;

using Microsoft.ML;
using Microsoft.ML.Transforms.Text;

namespace Florio.VectorEmbeddings.EmbeddingsModel;

internal class VectorEmbeddingModelFactory(MLContext mlContext) : IVectorEmbeddingModelFactory
{
    private readonly MLContext _mlContext = mlContext;
    private VectorEmbeddingModel? _model;

    public VectorEmbeddingModel GetModel() => _model is null
        ? ThrowInvalidOperation()
        : _model;

    public VectorEmbeddingModel CreateFromData(IEnumerable<string> data)
    {
        var dataView = _mlContext.Data.LoadFromEnumerable(data
            .Select(s => new InputStringData { Text = s }));

        var embeddingPipeline =
            _mlContext.Transforms.Text.TokenizeIntoCharactersAsKeys("CharTokens", "Text", useMarkerCharacters: true)
            .Append(_mlContext.Transforms.Text.ProduceNgrams("Features", "CharTokens",
                ngramLength: 3,
                useAllLengths: false,
                weighting: NgramExtractingEstimator.WeightingCriteria.Tf));

        var transformer = embeddingPipeline.Fit(dataView);

        _model = new VectorEmbeddingModel(_mlContext, transformer, dataView);
        return _model;
    }

    public VectorEmbeddingModel CreateFromOnnxFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File could not be found.", path);
        }

        var emptyDataView = _mlContext.Data.LoadFromEnumerable(new List<InputStringData>());

        var embeddingPipeline = _mlContext.Transforms.ApplyOnnxModel(modelFile: path);

        var transformer = embeddingPipeline.Fit(emptyDataView);

        _model = new VectorEmbeddingModel(_mlContext, transformer, emptyDataView);
        return _model;
    }

    [DoesNotReturn]
    private static VectorEmbeddingModel ThrowInvalidOperation()
    {
        throw new InvalidOperationException("A vector embedding model has not been created yet.");
    }
}
