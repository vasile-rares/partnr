// Starts the backend processes for development, with the Aspire dashboard (logs, traces and metrics of every service in one place).
// Not part of the deployment, and not in Partnr.slnx: building it needs the Aspire CLI. The infrastructure (Postgres, Mailpit,
// RustFS, RabbitMQ) stays in docker-compose.yml: start it first.
var builder = DistributedApplication.CreateBuilder(args);

// Each project keeps its own ports (Kestrel endpoints in its settings), so Aspire neither proxies nor reassigns them. Without the
// launch profile there is no Development environment either, and with it the development settings (connection strings, keys) are not read.
builder.AddDotnetProject("workspaces", "../Services/Workspaces/Partnr.Workspaces.Api/Partnr.Workspaces.Api.csproj", project =>
{
    project.ExcludeLaunchProfile = true;
})
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

builder.AddDotnetProject("integrations", "../Services/Integrations/Partnr.Integrations.Api/Partnr.Integrations.Api.csproj", project =>
{
    project.ExcludeLaunchProfile = true;
})
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

builder.AddDotnetProject("gateway", "../Gateway/Partnr.Gateway/Partnr.Gateway.csproj", project =>
{
    project.ExcludeLaunchProfile = true;
})
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_URLS", "http://localhost:5050");

builder.Build().Run();
