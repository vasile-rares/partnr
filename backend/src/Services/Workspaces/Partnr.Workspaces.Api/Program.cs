using System.Security.Claims;
using System.Threading.RateLimiting;
using Partnr.Workspaces.Api;
using Partnr.Workspaces.Api.Internal;
using Partnr.Workspaces.Api.Middleware;
using Partnr.Workspaces.Api.Realtime;
using Partnr.Workspaces.Application;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Scalar.AspNetCore;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Per signed-in user (per address when anonymous); answers 429.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Suggestions, context => RateLimitPartition.GetSlidingWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = RateLimitPolicies.SuggestionsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueLimit = 0,
        }));
    options.AddPolicy(RateLimitPolicies.PasswordReset, _ => RateLimitPartition.GetFixedWindowLimiter(
        RateLimitPolicies.PasswordReset,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = RateLimitPolicies.PasswordResetsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

builder.Services.AddSignalR();
builder.Services.AddGrpc();
// These replace the do-nothing ones AddInfrastructure leaves.
builder.Services.AddSingleton<IInboxPublisher, SignalRInboxPublisher>();
builder.Services.AddSingleton<IConversationPublisher, SignalRConversationPublisher>();

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

// `--seed` fills the development database and exits. Never automatic, so tests never get demo accounts.
if (args.Contains("--seed"))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("--seed only works in the Development environment.");
    }

    await app.Services.SeedDevelopmentDataAsync();
    return;
}

// Opt-in via the launch profile: the integration and e2e runs are Development too and must never recreate anything.
if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Seed:ReseedOnChange"))
{
    await app.Services.ReseedDevelopmentDataIfChangedAsync();
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
app.UseRateLimiter(); // after authentication, so the limit can follow the signed-in user
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationsHub>(NotificationsHub.Route);
app.MapGrpcService<SessionValidationService>();

app.Run();

// Visible to WebApplicationFactory<Program> in the integration tests.
public partial class Program;
