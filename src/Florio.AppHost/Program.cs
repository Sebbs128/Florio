using Azure.Provisioning.AppContainers;
using Azure.Provisioning.CosmosDB;

var builder = DistributedApplication.CreateBuilder(args);

var cosmosDb = builder.AddAzureCosmosDB("cosmos")
    .RunAsEmulator(config =>
    {
        //config.WithHttpsEndpoint(8081, 8081, "emulator-port"); // currently has network binding issues, has worked previously though
        config.WithDataVolume("Florio-CosmosDb");
        /*
         * requires RunAsPreviewEmulator instead of RunAsEmulator; Preview emulator doesn't support vector search yet
#pragma warning disable ASPIRECOSMOSDB001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        //config.WithDataExplorer(8081); 
#pragma warning restore ASPIRECOSMOSDB001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        */
        config.WithLifetime(ContainerLifetime.Persistent);
    })
    .ConfigureInfrastructure(config =>
    {
        var account = config.GetProvisionableResources()
            .OfType<CosmosDBAccount>()
            .Single();

        account.IsFreeTierEnabled = true;

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
var database = cosmosDb.AddCosmosDatabase("florio");

builder.AddAzureContainerAppEnvironment("env");

var dbManager = builder.AddProject<Projects.Florio_VectorDbManager>("vectordbmanager")
    .WithReference(cosmosDb)
    .WaitFor(cosmosDb)
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

builder.AddProject<Projects.Florio_WebApp>("webapp")
    .WithReference(cosmosDb)
    .WaitForCompletion(dbManager)
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp((resource, app) =>
    {
        var webDomain = builder.AddParameter("webDomain");
        var webCertificate = builder.AddParameter("webCertificate", value: "", publishValueAsDefault: true);

#pragma warning disable ASPIREACADOMAINS001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        app.ConfigureCustomDomain(webDomain, webCertificate);
#pragma warning restore ASPIREACADOMAINS001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    });

builder.Build().Run();
