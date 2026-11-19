using Microsoft.AspNetCore.Authorization;
using Partnr.Integrations.Api.Internal;
using Partnr.Integrations.Api.Middleware;
using Partnr.Integrations.Application;
using Partnr.Integrations.Infrastructure;
using Partnr.ServiceDefaults;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddInternalAuthentication();

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Wolverine looks for handlers in the assembly that called AddWolverine (Infrastructure); the consumers of events live here.
builder.Services.ConfigureWolverine(wolverine => wolverine.Discovery.IncludeAssembly(typeof(Program).Assembly));

builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<GoogleConnectionsGrpcService>();
app.MapDefaultEndpoints();

app.Run();

// Visible to WebApplicationFactory<Program> in the integration tests.
public partial class Program;
