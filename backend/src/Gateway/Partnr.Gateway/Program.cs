using Partnr.Contracts.Sessions;
using Partnr.Gateway.Sessions;
using Partnr.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddInternalAuthOptions();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<InternalTokenIssuer>();

// Sessions belong to the Workspaces service (it holds the accounts) until Identity is split out of it.
builder.Services.AddGrpcClient<SessionValidation.SessionValidationClient>(options =>
    options.Address = new Uri(builder.Configuration["Services:Workspaces:GrpcAddress"] ?? throw new InvalidOperationException("Services:Workspaces:GrpcAddress is missing.")));
builder.Services.AddTransient<ISessionValidator, GrpcSessionValidator>();

var app = builder.Build();

app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), api => api.UseMiddleware<SessionMiddleware>());
app.MapDefaultEndpoints();
app.MapReverseProxy();

app.Run();

// Visible to WebApplicationFactory<Program> in the gateway tests.
public partial class Program;
