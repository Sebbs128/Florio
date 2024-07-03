var builder = DistributedApplication.CreateBuilder(args);

var qdrant = builder.AddQdrant("qdrant")
    .WithImageTag("v1.10.0")
    .WithDataVolume();

builder.AddProject<Projects.Florio_VectorDbManager>("vectordbmanager")
    .WithReference(qdrant);

builder.AddProject<Projects.Florio_WebApp>("webapp")
    .WithReference(qdrant)
    .WithExternalHttpEndpoints();

builder.Build().Run();
