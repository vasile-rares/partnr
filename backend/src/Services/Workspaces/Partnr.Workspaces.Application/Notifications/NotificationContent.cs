using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;

namespace Partnr.Workspaces.Application.Notifications;

/// <summary>What a notification says and where it leads, in one place so the email (and later the inbox) never disagree.</summary>
internal sealed record NotificationContent(string Subject, string Headline, string ButtonLabel, string Path)
{
    public static NotificationContent For(Notification n) => n.Type switch
    {
        NotificationType.ApprovalRequested => new(
            $"{n.WorkspaceName} asked for your approval: {n.DeliverableTitle}",
            $"{n.WorkspaceName} is waiting for your approval of \"{n.DeliverableTitle}\" in {n.ProjectName}.",
            "Review and respond",
            $"/w/{n.WorkspaceId}/projects/{n.ProjectId}?tab=deliverables"),

        NotificationType.ApprovalApproved => new(
            $"{n.ActorName} approved {n.DeliverableTitle}",
            $"{n.ActorName} approved \"{n.DeliverableTitle}\" in {n.ProjectName}.",
            "Open the project",
            $"/w/{n.WorkspaceId}/projects/{n.ProjectId}?tab=deliverables"),

        NotificationType.ApprovalChangesRequested => new(
            $"{n.ActorName} requested changes to {n.DeliverableTitle}",
            $"{n.ActorName} asked for changes to \"{n.DeliverableTitle}\" in {n.ProjectName}.",
            "See what they asked for",
            $"/w/{n.WorkspaceId}/projects/{n.ProjectId}?tab=deliverables"),

        NotificationType.InvitationAccepted => new(
            $"{n.ActorName} joined your client portal",
            $"{n.ActorName} accepted your invitation and can now follow their projects in {n.WorkspaceName}.",
            "Open the client",
            $"/w/{n.WorkspaceId}/clients/{n.ClientId}"),

        _ => throw new ArgumentOutOfRangeException(nameof(n), n.Type, "Unknown notification type.")
    };
}
