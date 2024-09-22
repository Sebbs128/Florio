var builder = DistributedApplication.CreateBuilder(args);

//var cosmosdb = builder.AddConnectionString("cosmos");

var cosmosdb = builder.AddAzureCosmosDB("cosmos")
    .AddDatabase("cosmosdb");
if (!builder.ExecutionContext.IsPublishMode)
{
    cosmosdb.RunAsEmulator(config =>
    {
        config.WithHttpsEndpoint(8081, 8081, "emulator-port");
    });
}

var qdrant = builder.AddQdrant("qdrant")
    .WithImageTag("v1.10.0")
    .WithDataVolume();

builder.AddProject<Projects.Florio_VectorDbManager>("qdrantmanager", launchProfileName: null)
    .WithReference(qdrant);

builder.AddProject<Projects.Florio_VectorDbManager>("cosmosmanager", launchProfileName: null)
    .WithReference(cosmosdb);

builder.AddProject<Projects.Florio_TestApps_VectorSearchComparer>("searchcomparer")
    .WithReference(qdrant)
    .WithReference(cosmosdb);

builder.Build().Run();
