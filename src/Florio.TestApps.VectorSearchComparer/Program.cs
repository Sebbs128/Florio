using System.Text;

using Florio.TestApps.VectorSearchComparer;
using Florio.VectorEmbeddings.CosmosDb;
using Florio.VectorEmbeddings.Qdrant;

Console.OutputEncoding = Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddQdrantClient("qdrant");
builder.AddAzureCosmosClient("cosmos");

builder.Services.AddGutenbergDownloaderAndParser();
builder.Services.AddVectorEmbeddingsModel(builder.Configuration["EmbeddingsSettings:OnnxFilePath"]!)
    .AddVectorEmbeddingsRepository<QdrantRepository>("qdrant")
    .AddVectorEmbeddingsRepository<CosmosDbRepository>("cosmos")
    .AddHostedService<SearchComparerBackgroundService>();

var host = builder.Build();
host.Run();
