using Florio.VectorEmbeddings;
using Florio.VectorEmbeddings.Qdrant;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
//builder.AddQdrantClient("qdrant");
builder.AddAzureCosmosClient("cosmos", configureClientOptions: options =>
{
    //options.AllowBulkExecution = true;
    options.EnableContentResponseOnWrite = false;
    //options.RequestTimeout = TimeSpan.FromMinutes(5);
    options.MaxRetryAttemptsOnRateLimitedRequests = 5;
    options.MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(5);
});

builder.Services.AddGutenbergDownloaderAndParser();
builder.Services.AddVectorEmbeddingsModel(builder.Configuration["EmbeddingsSettings:OnnxFilePath"]!)
    //.AddVectorEmbeddingsRepository<QdrantRepository>()
    //.AddVectorEmbeddingsMigrations<QdrantMigrator>()
    .AddVectorEmbeddingsRepository<CosmosDbRepository>()
    .AddHostedService<VectorDbInitializerBackgroundService>();

var app = builder.Build();

app.Run();
