using Partnr.ServiceDefaults;
using Partnr.Storage.Api.Internal;
using Partnr.Storage.Application;
using Partnr.Storage.Infrastructure;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Wolverine looks for handlers in the assembly that called AddWolverine (Infrastructure); the consumers of events live here.
builder.Services.ConfigureWolverine(wolverine => wolverine.Discovery.IncludeAssembly(typeof(Program).Assembly));

builder.Services.AddGrpc();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync();
}

// No browser and no gateway route: the only callers are other services, on the internal gRPC port.
app.MapGrpcService<FilesGrpcService>();
app.MapDefaultEndpoints();

app.Run();

// Visible to WebApplicationFactory<Program> in the integration tests.
public partial class Program;
