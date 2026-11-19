namespace Partnr.Notifications.Domain.Enums;

public enum NotificationType
{
    /// <summary>To the client: the freelancer asked for their approval of a deliverable.</summary>
    ApprovalRequested,

    /// <summary>To the freelancer who asked: the client approved.</summary>
    ApprovalApproved,

    /// <summary>To the freelancer who asked: the client asked for changes.</summary>
    ApprovalChangesRequested,

    /// <summary>To the member who invited: the client accepted and can open the portal.</summary>
    InvitationAccepted
}
