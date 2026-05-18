using Backend.Shared;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("aca-env");

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume("carenestai-postgres-data");

var database = postgres.AddDatabase(Services.Database, databaseName: "carenestai");

var redis = builder
    .AddRedis(Services.Redis)
    .WithDataVolume("carenestai-redis-data");

builder.AddProject<Projects.Web>(Services.WebApi)
    .WithReference(database)
    .WithReference(redis)
    .WaitFor(database)
    .WaitFor(redis)
    .WithExternalHttpEndpoints()
    .WithAspNetCoreEnvironment()
    .WithUrlForEndpoint("http", url =>
    {
        url.DisplayText = "Swagger UI";
        url.Url = "/docs";
    });

builder.Build().Run();
