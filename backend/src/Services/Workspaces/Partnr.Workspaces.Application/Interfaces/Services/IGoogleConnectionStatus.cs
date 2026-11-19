namespace Partnr.Workspaces.Application.Interfaces.Services;

/// <summary>
/// Whether an account has a working Google connection. The connections belong to the Integrations service, so this asks it while a
/// person waits for the answer. Throws <see cref="Exceptions.ServiceUnavailableException"/> when it cannot be asked.
/// </summary>
public interface IGoogleConnectionStatus
{
    Task<bool> IsConnectedAsync(Guid userId, CancellationToken cancellationToken);
}
