namespace ClientPortal.Application.DTOs.Clients;

/// <summary>
/// A client as the workspace sees them. For a client who has an account, name, email, phone and company are the account's
/// own (they cannot diverge from it); the notes always belong to the workspace.
/// </summary>
/// <param name="DetailsLocked">
/// True while the client has a pending invitation or an account: name, email, phone and company can no longer be edited
/// here. Cancelling the invitation (or letting it expire) lifts the lock; once the client has an account it stays.
/// </param>
public sealed record ClientResponse(
    Guid Id,
    string Name,
    string Email,
    string? Company,
    string? Phone,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc,
    ClientPortalResponse Portal,
    bool DetailsLocked);

/// <param name="Status">One of <see cref="ClientPortalStatus"/>, as text.</param>
/// <param name="ExpiresAtUtc">When the open invitation expires (or expired); null when there is none.</param>
public sealed record ClientPortalResponse(string Status, DateTime? ExpiresAtUtc);

/// <summary>The client's access to the portal, from the workspace's point of view.</summary>
public enum ClientPortalStatus
{
    /// <summary>No invitation was sent, or it was cancelled.</summary>
    NotInvited,

    /// <summary>An invitation was sent and can still be accepted.</summary>
    Invited,

    /// <summary>The invitation was not accepted in time; send it again.</summary>
    Expired,

    /// <summary>The client accepted and has an account.</summary>
    Active
}
