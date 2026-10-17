namespace ClientPortal.Application.DTOs.Invitations;

/// <param name="AccountExists">An account with the invited email exists: sign in instead of signing up.</param>
public sealed record InvitationPreviewResponse(
    string WorkspaceName,
    string Email,
    string Status,
    bool AccountExists,
    DateTime ExpiresAtUtc);
