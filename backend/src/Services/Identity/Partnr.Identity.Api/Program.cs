using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Partnr.Identity.Api;
using Partnr.Identity.Api.Internal;
using Partnr.Identity.Api.Middleware;
using Partnr.Identity.Application;
using Partnr.Identity.Infrastructure;
using Partnr.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddInternalAuthentication();

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.PasswordReset, _ => RateLimitPartition.GetFixedWindowLimiter(
        RateLimitPolicies.PasswordReset,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = RateLimitPolicies.PasswordResetsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

builder.Services.AddGrpc();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseRateLimiter(); // after authentication, like the other services
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();
app.MapGrpcService<SessionValidationService>();

app.Run();

// Visible to WebApplicationFactory<Program> in the integration tests.
public partial class Program;
