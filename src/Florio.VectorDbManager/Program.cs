using Florio.VectorEmbeddings.Qdrant;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddQdrantClient("qdrant");

builder.Services.AddGutenbergDownloaderAndParser();
builder.Services.AddVectorEmbeddingsModel(builder.Configuration["EmbeddingsSettings:OnnxFilePath"]!)
    .AddVectorEmbeddingsRepository<QdrantRepository>()
    .AddVectorEmbeddingsInitializer();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.Run();
