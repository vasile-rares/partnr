using System.Collections.Concurrent;
using Partnr.Core.Application.Exceptions;
using Partnr.Core.Application.Interfaces.Services;

namespace Partnr.Core.IntegrationTests;

/// <summary>
/// Stands in for the Integrations service. Shared by all tests (they run in parallel), so each test uses its own account:
/// <see cref="Connect"/> gives that account a connected Google, and <see cref="Unreachable"/> makes the service "down" for it.
/// </summary>
public sealed class FakeGoogleConnectionStatus : IGoogleConnectionStatus
{
    private readonly ConcurrentDictionary<Guid, bool> _connected = new();
    private readonly ConcurrentDictionary<Guid, bool> _unreachable = new();

    public void Connect(Guid userId) => _connected[userId] = true;

    public void Disconnect(Guid userId) => _connected.TryRemove(userId, out _);

    public void Unreachable(Guid userId) => _unreachable[userId] = true;

    public Task<bool> IsConnectedAsync(Guid userId, CancellationToken cancellationToken) =>
        _unreachable.ContainsKey(userId)
            ? throw new ServiceUnavailableException("Integrations is down (fake).")
            : Task.FromResult(_connected.ContainsKey(userId));
}
