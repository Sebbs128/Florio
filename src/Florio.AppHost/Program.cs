using Azure.Provisioning.AppContainers;
using Azure.Provisioning.CosmosDB;

var builder = DistributedApplication.CreateBuilder(args);

var cosmosdb = builder.AddAzureCosmosDB("cosmos");
var database = cosmosdb.AddCosmosDatabase("florio");

if (!builder.ExecutionContext.IsPublishMode)
{
    cosmosdb.RunAsEmulator(config =>
    {
        //config.WithHttpsEndpoint(8081, 8081, "emulator-port");
        config.WithDataVolume();
#pragma warning disable ASPIRECOSMOSDB001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        config.WithDataExplorer(8081);
#pragma warning restore ASPIRECOSMOSDB001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        config.WithLifetime(ContainerLifetime.Persistent);
    });
}
else
{
    cosmosdb.ConfigureInfrastructure(config =>
    {
        var account = config.GetProvisionableResources()
            .OfType<CosmosDBAccount>()
            .Single();
        account.Locations ??= [];
        account.Locations.Add(new CosmosDBAccountLocation()
        {
            IsZoneRedundant = false
        });
        account.Capabilities ??= [];
        account.Capabilities.Add(new CosmosDBAccountCapability()
        {
            Name = "EnableServerless"
        });
        account.Capabilities.Add(new CosmosDBAccountCapability()
        {
            Name = "EnableNoSQLVectorSearch"
        });
    });
}

builder.AddAzureContainerAppEnvironment("env");

var dbManager = builder.AddProject<Projects.Florio_VectorDbManager>("vectordbmanager")
    .WithReference(cosmosdb)
    .WaitFor(cosmosdb)
    .PublishAsAzureContainerApp((resource, app) =>
    {
        app.Template.Scale.MinReplicas = 0;
        app.Template.Scale.MaxReplicas = 1;
        app.Template.Containers ??= [];
        app.Template.Containers.Add(new ContainerAppContainer()
        {
            Resources = new()
            {
                Cpu = 1.5,
                Memory = "3Gi"
            }
        });
    });

var webDomain = builder.AddParameter("webDomain");
var webCertificate = builder.AddParameter("webCertificate", value: "", publishValueAsDefault: true);

builder.AddProject<Projects.Florio_WebApp>("webapp")
    .WithReference(cosmosdb)
    .WaitForCompletion(dbManager)
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp((resource, app) =>
    {
#pragma warning disable ASPIREACADOMAINS001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        app.ConfigureCustomDomain(webDomain, webCertificate);
#pragma warning restore ASPIREACADOMAINS001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    });

builder.Build().Run();
