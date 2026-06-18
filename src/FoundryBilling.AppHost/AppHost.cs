var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume("foundry-billing-pgdata");
var db = postgres.AddDatabase("foundry-billing-db", "foundry-billing");

var api = builder.AddProject<Projects.FoundryBilling_Api>("api")
    .WithExternalHttpEndpoints()
    .WithReference(db);

builder.AddNpmApp("web", "../web", "dev", ["--", "--host", "0.0.0.0", "--port", "5173"])
    .WithHttpEndpoint(targetPort: 5173, port: 5173, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithReference(api);

builder.Build().Run();
