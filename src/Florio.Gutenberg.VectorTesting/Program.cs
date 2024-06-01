using System.Text.Json;

using Florio.Gutenberg.Parser;
using Florio.Gutenberg.VectorModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;
using Microsoft.SemanticKernel.Memory;

var services = new ServiceCollection()
    .AddSingleton<IGutenbergTextDownloader, GutenbergTextDownloader>()
    .AddSingleton<GutenbergTextParser>()
    .AddSingleton<MLContext>()
    .AddSingleton<EmbeddingsModelFactory>();

services.AddHttpClient<GutenbergTextDownloader>();
var serviceProvider = services.BuildServiceProvider();

var parser = serviceProvider.GetRequiredService<GutenbergTextParser>();
var wordDefinitions = await parser.ParseLines().ToListAsync();

var embeddingsModelFactory = serviceProvider.GetRequiredService<EmbeddingsModelFactory>();

EmbeddingsModel? modelGen = null;
const string onnxFilePath = "gutenberg-vector-embeddings.onnx";
if (File.Exists(onnxFilePath))
{
    try
    {
        modelGen = embeddingsModelFactory.CreateFromOnnxFile(onnxFilePath);
    }
    catch (Exception)
    {
        Console.WriteLine("Encountered an error when attempting to load existing .onnx file.");
        File.Delete(onnxFilePath);
    }
}

if (modelGen is null)
{
    modelGen = embeddingsModelFactory.CreateFromData(wordDefinitions
        .Select(wd => StringUtilities.GetPrintableNormalizedString(wd.Word)));

    try
    {
        modelGen.ExportToOnnx(onnxFilePath);
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine("Encountered an error when attempting to export the moddel to an .onnx file.");
        Console.WriteLine();
    }
}

#pragma warning disable SKEXP0001, SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var vectorStore = new VolatileMemoryStore();

const string mlNetVectorCollectionName = "mlnetvectors";

await vectorStore.CreateCollectionAsync(mlNetVectorCollectionName);
await vectorStore.UpsertBatchAsync(mlNetVectorCollectionName, wordDefinitions
    .Select(wd => MemoryRecord.LocalRecord(
        id: $"{wd.Word}_{Guid.NewGuid()}",
        text: wd.Word,
        description: wd.Definition,
        embedding: modelGen.CalculateVector(StringUtilities.GetPrintableNormalizedString(wd.Word)),
        additionalMetadata: wd.ReferencedWords is not null ? JsonSerializer.Serialize(wd.ReferencedWords) : null)))
    .CountAsync();

string[] tests =
[
    "abbellare",
    "Propórre",
    "Cóllerra", // Cóllera
];

foreach (var testWord in tests)
{
    var normalisedWord = StringUtilities.GetPrintableNormalizedString(testWord);
    (MemoryRecord Record, double Similarity)? bestMatch =
        await vectorStore.GetNearestMatchAsync(mlNetVectorCollectionName, modelGen.CalculateVector(normalisedWord));

    if (bestMatch.HasValue)
    {
        Console.WriteLine($"Best match for \"{testWord}\" was \"{bestMatch.Value.Record.Metadata.Text}\" (Similarity: {bestMatch.Value.Similarity}");
        Console.WriteLine($"{bestMatch.Value.Record.Metadata.Text}: {bestMatch.Value.Record.Metadata.Description}");
    }
    else
    {
        Console.WriteLine($"No best match for \"{testWord}\" was found.");
    }
    Console.WriteLine();
}


#pragma warning restore SKEXP0001, SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.