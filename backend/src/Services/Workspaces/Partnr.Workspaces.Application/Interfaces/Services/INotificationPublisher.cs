using Partnr.Workspaces.Domain.Entities;

namespace Partnr.Workspaces.Application.Interfaces.Services;

/// <summary>
/// The one door through which services say that something happened that someone should hear about. It decides who, because only
/// this service knows (who may open a project, who is still a member), and publishes an event that names them: the Notifications
/// service stores, emails and pushes. Like every publisher it is called before <c>SaveChanges</c>, so the event leaves exactly when
/// the change it is about is saved.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>Tells the client, unless the project is not visible to them (no account yet, archived card, still in Planning).</summary>
    Task ApprovalRequestedAsync(string workspaceName, Project project, Deliverable deliverable, ApprovalRequest round, CancellationToken cancellationToken);

    /// <summary>Tells the member who asked, if they are still in the workspace.</summary>
    Task ApprovalDecidedAsync(Project project, Deliverable deliverable, ApprovalRequest round, CancellationToken cancellationToken);

    /// <summary>Tells the member who sent the invitation, if they are still in the workspace.</summary>
    Task InvitationAcceptedAsync(Workspace workspace, Client client, ClientInvitation invitation, Guid accountId, CancellationToken cancellationToken);

    /// <summary>The request was taken back: what was said about it no longer holds.</summary>
    Task ApprovalRequestWithdrawnAsync(Guid approvalRequestId, CancellationToken cancellationToken);
}
