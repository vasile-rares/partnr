using Partnr.Integrations.Application.Interfaces.Repositories;
using Partnr.Integrations.Application.Interfaces.Services;
using Partnr.Integrations.Domain.Entities;

namespace Partnr.Integrations.UnitTests.Application;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeCurrentUser(Guid? userId = null) : ICurrentUser
{
    public Guid? UserId { get; set; } = userId;
}

internal sealed class FakeTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakeGoogleConnectionRepository : IGoogleConnectionRepository
{
    public List<GoogleConnection> Items { get; } = [];

    public Task<GoogleConnection?> GetForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(c => c.UserId == userId));

    public void Add(GoogleConnection connection) => Items.Add(connection);

    public void Remove(GoogleConnection connection) => Items.Remove(connection);
}

internal sealed class FakeGoogleOAuthClient : IGoogleOAuthClient
{
    public bool IsConfigured { get; set; } = true;

    /// <summary>What Google answers to a code; null is a refusal.</summary>
    public GoogleGrant? Grant { get; set; } = new("refresh-1", "ana@gmail.example", true);

    public List<string> Revoked { get; } = [];

    public string AuthorizationUrl(string state) => $"https://google.test/consent?state={state}";

    public Task<GoogleGrant?> ExchangeCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult(Grant);

    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        Revoked.Add(refreshToken);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSecretProtector : ISecretProtector
{
    public string Protect(string purpose, string plaintext) => $"{purpose}:{plaintext}".Replace('|', '~');

    public string? Unprotect(string purpose, string protectedValue) =>
        protectedValue.StartsWith($"{purpose}:", StringComparison.Ordinal)
            ? protectedValue[(purpose.Length + 1)..].Replace('~', '|')
            : null;
}
