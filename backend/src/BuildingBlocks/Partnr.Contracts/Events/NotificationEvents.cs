namespace Partnr.Contracts.Events;

// What happened that an account should hear about. The service where it happened decides who (and only it knows how to), so each event
// names its recipient with everything needed to write to them (the account's email and name as they were at that moment): the
// Notifications service never has to ask back. Delivery is at least once.

/// <summary>A member asked the client to approve a deliverable.</summary>
public sealed record ApprovalRequested(
    Guid ApprovalRequestId,
    Guid WorkspaceId,
    string WorkspaceName,
    Guid ProjectId,
    string ProjectName,
    string DeliverableTitle,
    Guid RecipientUserId,
    string RecipientEmail,
    string RecipientName);

public enum ApprovalDecision
{
    Approved,
    ChangesRequested,
}

/// <summary>The client answered a request for approval; the recipient is the member who asked.</summary>
public sealed record ApprovalDecided(
    Guid ApprovalRequestId,
    ApprovalDecision Decision,
    Guid WorkspaceId,
    string WorkspaceName,
    Guid ProjectId,
    string ProjectName,
    string DeliverableTitle,
    string ActorName,
    Guid RecipientUserId,
    string RecipientEmail,
    string RecipientName);

/// <summary>A client accepted the invitation; the recipient is the member who sent it.</summary>
public sealed record InvitationAccepted(
    Guid WorkspaceId,
    string WorkspaceName,
    Guid ClientId,
    string ActorName,
    Guid RecipientUserId,
    string RecipientEmail,
    string RecipientName);

/// <summary>Someone accepted an invitation to the team; the recipient is the owner who sent it.</summary>
public sealed record MemberJoined(
    Guid WorkspaceId,
    string WorkspaceName,
    Guid MemberUserId,
    string ActorName,
    Guid RecipientUserId,
    string RecipientEmail,
    string RecipientName);

/// <summary>The request was taken back (withdrawn, or its deliverable unshared or archived): what was said about it no longer holds.</summary>
public sealed record ApprovalRequestWithdrawn(Guid ApprovalRequestId);

/// <summary>A project's conversation changed. Only a live push comes of it: the browsers of these accounts reload it.</summary>
public sealed record ConversationChanged(Guid ProjectId, IReadOnlyList<Guid> UserIds);
