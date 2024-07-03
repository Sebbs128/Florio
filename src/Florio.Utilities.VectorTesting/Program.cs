using System.Text;

using Florio.Utilities.VectorTesting;
using Florio.VectorEmbeddings.Extensions;
using Florio.VectorEmbeddings.SKInMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.OutputEncoding = Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddGutenbergDownloaderAndParser(@".localassets\pg56200.txt")
    .AddVectorEmbeddingsModel("Embeddings/ModelFiles/word-embeddings.onnx")
    .AddVectorEmbeddingsRepository<SemanticKernelMemoryRepository>()
    .AddVectorEmbeddingsInitializer()
    .AddHostedService<TestEmbeddingsService>();

var host = builder.Build();

await host.RunAsync();
