namespace Florio.VectorEmbeddings;
public class EmbeddingsSettings
{
    public string OnnxFilePath { get; set; } = string.Empty;
    public string CollectionName { get; set; } = "florio";
    public double ScoreThreshold { get; set; } = 0.75;
    public int? MaximumVectorCount { get; set; }
}
