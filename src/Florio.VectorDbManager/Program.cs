using Florio.VectorEmbeddings;
using Florio.VectorEmbeddings.Qdrant;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddQdrantClient("qdrant");

builder.Services.AddGutenbergDownloaderAndParser();
builder.Services.AddVectorEmbeddingsModel(builder.Configuration["EmbeddingsSettings:OnnxFilePath"]!)
    .AddVectorEmbeddingsRepository<QdrantRepository>()
    .AddVectorEmbeddingsMigrations<QdrantMigrator>()
    .AddHostedService<VectorDbInitializerBackgroundService>();

var app = builder.Build();

app.Run();
