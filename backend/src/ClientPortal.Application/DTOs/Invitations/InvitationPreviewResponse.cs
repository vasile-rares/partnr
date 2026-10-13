namespace ClientPortal.Application.DTOs.Invitations;

/// <summary>What the invitation page may show to whoever holds the link.</summary>
/// <param name="Status">One of <c>Pending</c>, <c>Expired</c>, <c>Cancelled</c>, <c>Accepted</c>.</param>
/// <param name="AccountExists">Whether an account with the invited email exists: sign in instead of signing up.</param>
public sealed record InvitationPreviewResponse(
    string WorkspaceName,
    string Email,
    string Status,
    bool AccountExists,
    DateTime ExpiresAtUtc);
