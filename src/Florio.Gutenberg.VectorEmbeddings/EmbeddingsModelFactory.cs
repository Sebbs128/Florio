using System.Data;

using Florio.Gutenberg.VectorModel.ModelData;

using Microsoft.ML;
using Microsoft.ML.Transforms.Text;

namespace Florio.Gutenberg.VectorModel
{
    public class EmbeddingsModelFactory
    {
        private readonly MLContext _mlContext;

        public EmbeddingsModelFactory(MLContext mlContext)
        {
            _mlContext = mlContext;
        }

        public EmbeddingsModel CreateFromData(IEnumerable<string> data)
        {
            var dataView = _mlContext.Data.LoadFromEnumerable(data
                .Select(s => new InputStringData { Text = s }));

            var embeddingPipeline =
                _mlContext.Transforms.Text.TokenizeIntoCharactersAsKeys("CharTokens", "Text", useMarkerCharacters: false)
                    .Append(_mlContext.Transforms.Conversion.MapValueToKey("CharTokens"))
                    .Append(_mlContext.Transforms.Text.ProduceNgrams("Features", "CharTokens",
                        ngramLength: 3,
                        useAllLengths: false,
                        weighting: NgramExtractingEstimator.WeightingCriteria.Tf));

            var transformer = embeddingPipeline.Fit(dataView);

            return new EmbeddingsModel(_mlContext, transformer, dataView);
        }

        public EmbeddingsModel CreateFromOnnxFile(string path)
        {
            var estimator = _mlContext.Transforms.ApplyOnnxModel(path);
            var emptyDataView = _mlContext.Data.LoadFromEnumerable(new InputStringData[] { });
            var transformer = estimator.Fit(emptyDataView);

            return new EmbeddingsModel(_mlContext, transformer, emptyDataView);
        }
    }
}
