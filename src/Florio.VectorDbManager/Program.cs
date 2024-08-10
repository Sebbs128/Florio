using Florio.VectorEmbeddings.CosmosDb;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddQdrantClient("qdrant");

builder.Services.AddGutenbergDownloaderAndParser();
builder.Services.AddVectorEmbeddingsModel(builder.Configuration["EmbeddingsSettings:OnnxFilePath"]!)
    .AddVectorEmbeddingsRepository<QdrantRepository>()
    .AddVectorEmbeddingsInitializer();

var app = builder.Build();

app.Run();
