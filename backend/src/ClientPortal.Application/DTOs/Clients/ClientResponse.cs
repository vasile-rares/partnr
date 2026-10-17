namespace ClientPortal.Application.DTOs.Clients;

/// <summary>For a client with an account, name, email, phone and company are the account's own; the notes are the workspace's.</summary>
/// <param name="DetailsLocked">True while an invitation is pending or the client has an account.</param>
public sealed record ClientResponse(
    Guid Id,
    string? Name,
    string Email,
    string? Company,
    string? Phone,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc,
    ClientPortalResponse Portal,
    bool DetailsLocked);

/// <param name="Status">One of <see cref="ClientPortalStatus"/>, as text.</param>
public sealed record ClientPortalResponse(string Status, DateTime? ExpiresAtUtc);

public enum ClientPortalStatus
{
    NotInvited,

    Invited,

    Expired,

    Active
}
