namespace Partnr.Core.Application.DTOs.Invitations;

/// <param name="AccountExists">An account with the invited email exists: sign in instead of signing up.</param>
/// <param name="Kind"><c>Client</c> (a client's portal) or <c>Member</c> (the workspace's team).</param>
public sealed record InvitationPreviewResponse(
    string WorkspaceName,
    string Email,
    string Status,
    bool AccountExists,
    DateTime ExpiresAtUtc,
    string Kind);
