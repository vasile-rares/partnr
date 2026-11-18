using Partnr.Workspaces.Domain.Entities;

namespace Partnr.Workspaces.Application.Interfaces.Services;

/// <summary>
/// The one door through which services record that something happened. It only adds rows: the caller saves, so the
/// notification exists exactly when the change it is about does.
/// </summary>
public interface INotificationWriter
{
    /// <summary>Tells the client, unless the project is not visible to them (no account yet, archived card, still in Planning).</summary>
    Task ApprovalRequestedAsync(string workspaceName, Project project, Deliverable deliverable, ApprovalRequest round, CancellationToken cancellationToken);

    /// <summary>Tells the member who asked, if they are still in the workspace.</summary>
    Task ApprovalDecidedAsync(Project project, Deliverable deliverable, ApprovalRequest round, CancellationToken cancellationToken);

    /// <summary>Tells the member who sent the invitation, if they are still in the workspace.</summary>
    Task InvitationAcceptedAsync(Workspace workspace, Client client, ClientInvitation invitation, Guid accountId, CancellationToken cancellationToken);

    /// <summary>The request was taken back: its notification leaves the inbox and its email, if it has not gone out yet, is cancelled.</summary>
    Task ApprovalRequestWithdrawnAsync(Guid approvalRequestId, CancellationToken cancellationToken);
}
