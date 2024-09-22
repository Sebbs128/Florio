using Florio.Data;
using Florio.VectorEmbeddings;
using Florio.VectorEmbeddings.EmbeddingsModel;

using Microsoft.Extensions.DependencyInjection;

string baseDataOutputPath = @"../../../Data";
string onnxModelRelativePath = $"{baseDataOutputPath}/word-embeddings.onnx";
string onnxModelPath = GetAbsolutePath(onnxModelRelativePath);

var services = new ServiceCollection()
    .AddGutenbergDownloaderAndParser(@".localassets\pg56200.txt")
    .AddVectorEmbeddingsTrainer();

services.AddOptions<EmbeddingsSettings>();

var serviceProvider = services.BuildServiceProvider();

var stringFormatter = serviceProvider.GetRequiredService<IStringFormatter>();
var parser = serviceProvider.GetRequiredService<IWordDefinitionParser>();
var wordDefinitions = await parser.ParseLines().ToListAsync();

var data = wordDefinitions
    .Select(wd => stringFormatter.NormalizeForVector(wd.Word))
    .Distinct();

var trainer = serviceProvider.GetRequiredService<VectorEmbeddingModelTrainer>();

await trainer.TrainAndSaveModel(data, onnxModelPath);

static string GetAbsolutePath(string relativePath)
{
    var root = new FileInfo(typeof(Program).Assembly.Location);
    var folderPath = root.Directory!.FullName;

    string fullPath = Path.Combine(folderPath, relativePath);

    if (!Path.Exists(fullPath))
    {
        if (Path.HasExtension(fullPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        }
        else
        {
            Directory.CreateDirectory(fullPath);
        }
    }

    return fullPath;
}