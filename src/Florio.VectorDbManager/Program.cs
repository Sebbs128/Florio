using Florio.VectorEmbeddings;
using Florio.VectorEmbeddings.CosmosDb;
using Florio.VectorEmbeddings.Qdrant;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddGutenbergDownloaderAndParser()
    .AddVectorEmbeddingsModel(builder.Configuration["EmbeddingsSettings:OnnxFilePath"]!)
    .AddHostedService<VectorDbInitializerBackgroundService>();

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

var app = builder.Build();

app.Run();
