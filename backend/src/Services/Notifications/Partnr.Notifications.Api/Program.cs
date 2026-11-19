using Microsoft.AspNetCore.Authorization;
using Partnr.Notifications.Api.Middleware;
using Partnr.Notifications.Api.Realtime;
using Partnr.Notifications.Application;
using Partnr.Notifications.Application.Interfaces.Services;
using Partnr.Notifications.Infrastructure;
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

builder.Services.AddSignalR();
// Only the Api knows SignalR: the services tell browsers through these ports.
builder.Services.AddSingleton<IInboxPublisher, SignalRInboxPublisher>();
builder.Services.AddSingleton<IConversationPublisher, SignalRConversationPublisher>();

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
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationsHub>(NotificationsHub.Route);
app.MapDefaultEndpoints();

app.Run();

// Visible to WebApplicationFactory<Program> in the integration tests.
public partial class Program;
