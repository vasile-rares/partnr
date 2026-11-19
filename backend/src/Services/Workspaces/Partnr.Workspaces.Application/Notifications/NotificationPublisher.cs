using Partnr.Contracts.Events;
using Partnr.Workspaces.Application.DTOs.Auth;
using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;

namespace Partnr.Workspaces.Application.Notifications;

internal sealed class NotificationPublisher(
    IEventPublisher events,
    IClientRepository clients,
    IWorkspaceRepository workspaces,
    IIdentityService identity) : INotificationPublisher
{
    public async Task ApprovalRequestedAsync(
        string workspaceName,
        Project project,
        Deliverable deliverable,
        ApprovalRequest round,
        CancellationToken cancellationToken)
    {
        var client = await clients.GetByIdAsync(project.WorkspaceId, project.ClientId, cancellationToken);

        // Someone who cannot open the project must not be told about it.
        if (!project.IsOpenToClient(client))
        {
            return;
        }

        var recipient = await identity.FindByIdAsync(client.UserId!.Value, cancellationToken);
        if (recipient is null)
        {
            return;
        }

        await events.PublishAsync(
            new ApprovalRequested(
                round.Id,
                project.WorkspaceId,
                workspaceName,
                project.Id,
                project.Name,
                deliverable.Title,
                recipient.Id,
                recipient.Email,
                DisplayName(recipient) ?? string.Empty),
            cancellationToken);
    }

    public async Task ApprovalDecidedAsync(Project project, Deliverable deliverable, ApprovalRequest round, CancellationToken cancellationToken)
    {
        var workspace = await workspaces.GetByIdAsync(project.WorkspaceId, cancellationToken);
        if (workspace is null || workspace.RoleOf(round.RequestedByUserId) is null)
        {
            return;
        }

        var recipient = await identity.FindByIdAsync(round.RequestedByUserId, cancellationToken);
        if (recipient is null)
        {
            return;
        }

        var decidedBy = round.DecidedByUserId is { } id ? await identity.FindByIdAsync(id, cancellationToken) : null;
        var decision = round.Status switch
        {
            ApprovalStatus.Approved => ApprovalDecision.Approved,
            ApprovalStatus.ChangesRequested => ApprovalDecision.ChangesRequested,
            _ => throw new InvalidOperationException("Only a decision is worth telling the freelancer about."),
        };

        await events.PublishAsync(
            new ApprovalDecided(
                round.Id,
                decision,
                workspace.Id,
                workspace.Name,
                project.Id,
                project.Name,
                deliverable.Title,
                DisplayName(decidedBy) ?? "Your client",
                recipient.Id,
                recipient.Email,
                DisplayName(recipient) ?? string.Empty),
            cancellationToken);
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

        var recipient = await identity.FindByIdAsync(invitation.InvitedByUserId, cancellationToken);
        if (recipient is null)
        {
            return;
        }

        var account = await identity.FindByIdAsync(accountId, cancellationToken);

        await events.PublishAsync(
            new InvitationAccepted(
                workspace.Id,
                workspace.Name,
                client.Id,
                DisplayName(account) ?? client.Name ?? client.Email,
                recipient.Id,
                recipient.Email,
                DisplayName(recipient) ?? string.Empty),
            cancellationToken);
    }

    public async Task ClientInvitedAsync(
        string workspaceName,
        Client client,
        ClientInvitation invitation,
        string token,
        CancellationToken cancellationToken)
    {
        var inviter = await identity.FindByIdAsync(invitation.InvitedByUserId, cancellationToken);

        await events.PublishAsync(
            new ClientInvited(
                invitation.Email,
                client.Name ?? string.Empty,
                workspaceName,
                inviter?.FullName ?? string.Empty,
                token,
                invitation.ExpiresAtUtc),
            cancellationToken);
    }

    public Task PasswordResetRequestedAsync(CurrentUserResponse account, string token, CancellationToken cancellationToken) =>
        events.PublishAsync(new PasswordResetRequested(account.Email, account.FullName, token), cancellationToken).AsTask();

    public Task ApprovalRequestWithdrawnAsync(Guid approvalRequestId, CancellationToken cancellationToken) =>
        events.PublishAsync(new ApprovalRequestWithdrawn(approvalRequestId), cancellationToken).AsTask();

    private static string? DisplayName(CurrentUserResponse? account)
    {
        if (account is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(account.FullName) ? account.Email : account.FullName;
    }
}
