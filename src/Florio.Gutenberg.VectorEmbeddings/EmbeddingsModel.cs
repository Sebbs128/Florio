using Florio.Gutenberg.VectorModel.ModelData;

using Microsoft.ML;

namespace Florio.Gutenberg.VectorModel
{
    public class EmbeddingsModel
    {
        private readonly MLContext _mlContext;
        private readonly ITransformer _transformer;
        private readonly IDataView _dataView;
        private readonly PredictionEngine<InputStringData, OutputVectorData> _embeddingGenerator;

        public EmbeddingsModel(MLContext mlContext, ITransformer transformer, IDataView dataView)
        {
            _mlContext = mlContext;
            _transformer = transformer;
            _dataView = dataView;

            _embeddingGenerator = mlContext.Model.CreatePredictionEngine<InputStringData, OutputVectorData>(transformer);
        }

        public ReadOnlyMemory<float> CalculateVector(string text)
        {
            var output = _embeddingGenerator.Predict(new InputStringData { Text = text });
            return new ReadOnlyMemory<float>(output.Features);
        }

        public void ExportToOnnx(string path)
        {
            using var fileStream = File.Create(path);
            _mlContext.Model.ConvertToOnnx(_transformer, _dataView, fileStream, nameof(OutputVectorData.OriginalText), nameof(OutputVectorData.Features));
        }
    }
}
