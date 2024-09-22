using Florio.VectorEmbeddings;
using Florio.VectorEmbeddings.CosmosDb;
using Florio.VectorEmbeddings.Qdrant;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

if (builder.Configuration.GetConnectionString("qdrant") is { Length: > 0 })
{
    builder.AddQdrantClient("qdrant");
    builder.Services.AddVectorEmbeddingsRepository<QdrantRepository>()
        .AddVectorEmbeddingsMigrations<QdrantMigrator>();
}
if (builder.Configuration.GetConnectionString("cosmos") is { Length: > 0 })
{
    builder.AddAzureCosmosClient("cosmos", configureClientOptions: options =>
    {
        //options.AllowBulkExecution = true;
        options.EnableContentResponseOnWrite = false;
        //options.RequestTimeout = TimeSpan.FromMinutes(5);
        options.MaxRetryAttemptsOnRateLimitedRequests = 5;
        options.MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(5);
    });
    builder.Services.AddVectorEmbeddingsRepository<CosmosDbRepository>()
    .AddVectorEmbeddingsMigrations<CosmosDbMigrator>();
}

builder.Services.AddGutenbergDownloaderAndParser();
builder.Services.AddVectorEmbeddingsModel(builder.Configuration["EmbeddingsSettings:OnnxFilePath"]!)
    //.AddVectorEmbeddingsRepository<QdrantRepository>()
    //.AddVectorEmbeddingsMigrations<QdrantMigrator>()
    //.AddVectorEmbeddingsRepository<CosmosDbRepository>()
    //.AddVectorEmbeddingsMigrations<CosmosDbMigrator>()
    .AddHostedService<VectorDbInitializerBackgroundService>();

var app = builder.Build();

app.Run();
