using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Partnr.Gateway.Tests;

/// <summary>Stands in for the API behind the gateway: answers with what it received, and sets a cookie like a sign-in would.</summary>
internal sealed class FakeApi : IAsyncDisposable
{
    private readonly WebApplication _app;
    private int _requests;

    private FakeApi(WebApplication app) => _app = app;

    public string Address { get; private set; } = "";

    public int Requests => _requests;

    /// <summary>The host (without the port) of the last request: the gateway's own when it passes the original Host on.</summary>
    public string? LastHost { get; private set; }

    public static async Task<FakeApi> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var api = new FakeApi(app);

        app.Map("/{**path}", async (HttpContext context) =>
        {
            Interlocked.Increment(ref api._requests);
            api.LastHost = context.Request.Host.Host;

            long length = 0;
            var buffer = new byte[81920];
            int read;
            while ((read = await context.Request.Body.ReadAsync(buffer)) > 0)
            {
                length += read;
            }

            context.Response.Headers.SetCookie = "session=from-api; path=/; httponly";
            return Results.Json(new Received(
                context.Request.Method,
                context.Request.Path + context.Request.QueryString.Value,
                context.Request.Headers.Cookie.ToString(),
                length,
                context.Request.Headers.Authorization.ToString()));
        });

        await app.StartAsync();
        api.Address = app.Urls.First();
        return api;
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}

internal sealed record Received(string Method, string PathAndQuery, string Cookie, long Length, string Authorization);
