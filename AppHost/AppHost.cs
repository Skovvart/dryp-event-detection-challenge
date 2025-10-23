var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddDockerComposeEnvironment("docker-compose")
    .WithDashboard()
    .ConfigureComposeFile(c =>
    {
        c.Services["docker-compose-dashboard"].Ports.Add("18888:18888");
        c.Services["api"].Ports.Clear();
        c.Services["api"].Ports.Add("${API_PORT}:${API_PORT}");
    });

builder
    .AddProject<Projects.Api>("api")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/_health")
    .PublishAsDockerComposeService(static (res, service) => service.Name = "api");

builder.Build().Run();
