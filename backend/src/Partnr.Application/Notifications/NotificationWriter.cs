using Partnr.Application.DTOs.Auth;
using Partnr.Application.Interfaces.Repositories;
using Partnr.Application.Interfaces.Services;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;

namespace Partnr.Application.Notifications;

internal sealed class NotificationWriter(
    INotificationRepository notifications,
    IClientRepository clients,
    IWorkspaceRepository workspaces,
    IIdentityService identity,
    TimeProvider time) : INotificationWriter
{
    public async Task ApprovalRequestedAsync(
        string workspaceName,
        Project project,
        Deliverable deliverable,
        ApprovalRequest round,
        CancellationToken cancellationToken)
    {
        var client = await clients.GetByIdAsync(project.WorkspaceId, project.ClientId, cancellationToken);

        // The portal's own rule: someone who cannot open the project must not be told about it.
        if (client is null || client.UserId is not { } clientUserId || client.IsArchived
            || project.IsArchived || project.Status == ProjectStatus.Planning)
        {
            return;
        }

        notifications.Add(Notification.ApprovalRequested(
            clientUserId, project.WorkspaceId, workspaceName, project.Id, project.Name, deliverable.Title, round.Id));
    }

    public async Task ApprovalDecidedAsync(Project project, Deliverable deliverable, ApprovalRequest round, CancellationToken cancellationToken)
    {
        var workspace = await workspaces.GetByIdAsync(project.WorkspaceId, cancellationToken);
        if (workspace is null || workspace.RoleOf(round.RequestedByUserId) is null)
        {
            return;
        }

        var account = round.DecidedByUserId is { } decidedBy ? await identity.FindByIdAsync(decidedBy, cancellationToken) : null;

        notifications.Add(Notification.ApprovalDecided(
            round.Status,
            round.RequestedByUserId,
            workspace.Id,
            workspace.Name,
            project.Id,
            project.Name,
            deliverable.Title,
            round.Id,
            DisplayName(account) ?? "Your client"));
    }

    public async Task InvitationAcceptedAsync(
        Workspace workspace,
        Client client,
        ClientInvitation invitation,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        if (workspace.RoleOf(invitation.InvitedByUserId) is null)
        {
            return;
        }

        var account = await identity.FindByIdAsync(accountId, cancellationToken);

        notifications.Add(Notification.InvitationAccepted(
            invitation.InvitedByUserId, workspace.Id, workspace.Name, client.Id, DisplayName(account) ?? client.Name ?? client.Email));
    }

    public async Task ApprovalRequestWithdrawnAsync(Guid approvalRequestId, CancellationToken cancellationToken) =>
        (await notifications.GetApprovalRequestedAsync(approvalRequestId, cancellationToken))?.MarkObsolete(time.GetUtcNow().UtcDateTime);

    private static string? DisplayName(CurrentUserResponse? account)
    {
        if (account is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(account.FullName) ? account.Email : account.FullName;
    }
}
