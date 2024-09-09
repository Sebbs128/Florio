using System.Text;

using Florio.Utilities.VectorTesting;
using Florio.VectorEmbeddings.SKInMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel.Memory;

Console.OutputEncoding = Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddGutenbergDownloaderAndParser(@".localassets\pg56200.txt")
    .AddVectorEmbeddingsModel("Embeddings/ModelFiles/word-embeddings.onnx")
    .AddVectorEmbeddingsRepository<SemanticKernelMemoryRepository>()
    .AddVectorEmbeddingsMigrations<SemanticKernelMemoryMigrator>()
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    .AddSingleton<VolatileMemoryStore>()
#pragma warning restore SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    .AddHostedService<TestEmbeddingsService>();

var host = builder.Build();

await host.RunAsync();
