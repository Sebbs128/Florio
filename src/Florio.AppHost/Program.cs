var builder = DistributedApplication.CreateBuilder(args);

var cosmosdb = builder.AddAzureCosmosDB("cosmos")
    .AddDatabase("cosmosdb")
    .RunAsEmulator(config =>
    {
        config.WithHttpsEndpoint(8081, 8081, "emulator-port");
        config.WithEnvironment(ctx =>
        {
            ctx.EnvironmentVariables.Add("AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE", "true");
        });
    });

builder.AddProject<Projects.Florio_VectorDbManager>("vectordbmanager")
    .WithReference(cosmosdb);

builder.AddProject<Projects.Florio_WebApp>("webapp")
    .WithReference(cosmosdb)
    .WithExternalHttpEndpoints();

builder.Build().Run();
