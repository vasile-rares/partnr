using System.Collections.Concurrent;
using Partnr.Workspaces.Application.Interfaces.Services;

namespace Partnr.Workspaces.IntegrationTests;

/// <summary>Stands in for Google. Shared by all tests (they run in parallel), so each test uses its own code: <see cref="Grants"/> maps a code to what Google would answer.</summary>
public sealed class FakeGoogleOAuthClient : IGoogleOAuthClient
{
    public ConcurrentDictionary<string, GoogleGrant> Grants { get; } = new();

    public ConcurrentBag<string> Revoked { get; } = [];

    public bool IsConfigured => true;

    public string AuthorizationUrl(string state) => $"https://google.test/consent?state={Uri.EscapeDataString(state)}";

    public Task<GoogleGrant?> ExchangeCodeAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(Grants.GetValueOrDefault(code));

    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        Revoked.Add(refreshToken);
        return Task.CompletedTask;
    }
}
