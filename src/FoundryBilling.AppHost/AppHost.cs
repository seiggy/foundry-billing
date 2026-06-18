var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.FoundryBilling_Api>("api")
    .WithExternalHttpEndpoints();

builder.AddNpmApp("web", "../web", "dev", ["--", "--host", "0.0.0.0", "--port", "5173"])
    .WithHttpEndpoint(targetPort: 5173, port: 5173, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithReference(api);

builder.Build().Run();
