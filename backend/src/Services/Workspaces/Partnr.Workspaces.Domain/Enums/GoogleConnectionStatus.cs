namespace Partnr.Workspaces.Domain.Enums;

public enum GoogleConnectionStatus
{
    Connected,

    /// <summary>Google no longer accepts the saved permission (revoked, expired or changed): the person has to connect again.</summary>
    NeedsReconnect,
}
