var builder = DistributedApplication.CreateBuilder(args);

var cosmosdb = builder.AddAzureCosmosDB("cosmos")
    .AddDatabase("cosmosdb")
    .RunAsEmulator(config =>
    {
        config.WithHttpsEndpoint(8081, 8081, "emulator-port");
    });

var qdrant = builder.AddQdrant("qdrant")
    .WithImageTag("v1.10.0")
    .WithDataVolume();

builder.AddProject<Projects.Florio_VectorDbManager>("vectordbmanager")
    //.WithReference(qdrant);
    .WithReference(cosmosdb);

builder.AddProject<Projects.Florio_WebApp>("webapp")
    //.WithReference(qdrant)
    .WithReference(cosmosdb)
    .WithExternalHttpEndpoints();

builder.Build().Run();
