using ClientPortal.Api.Middleware;
using ClientPortal.Application;
using ClientPortal.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Scalar.AspNetCore;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Every endpoint requires a signed-in user unless it opts out with [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials())); // the browser only sends the auth cookie cross-origin with this

var app = builder.Build();

// `dotnet run -- --seed` fills the development database with test data and exits. Never on its own, so tests and
// other environments never get demo accounts by accident.
if (args.Contains("--seed"))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("--seed only works in the Development environment.");
    }

    await app.Services.SeedDevelopmentDataAsync();
    return;
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Makes the entry point visible to WebApplicationFactory<Program> in the integration tests.
public partial class Program;
