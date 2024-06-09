var builder = DistributedApplication.CreateBuilder(args);

var qdrant = builder.AddQdrant("qdrant")
    .WithDataVolume();

builder.AddProject<Projects.Florio_WebApp>("webApp")
    .WithReference(qdrant);

builder.Build().Run();
