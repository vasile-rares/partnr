namespace ClientPortal.Application.DTOs.Invitations;

/// <summary>The workspace the client is now connected to.</summary>
public sealed record AcceptedInvitationResponse(Guid WorkspaceId, string WorkspaceName);
