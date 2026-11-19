using Partnr.Workspaces.Application.Authorization;
using Partnr.Workspaces.Application.DTOs.Approvals;
using Partnr.Workspaces.Application.DTOs.Deliverables;
using Partnr.Workspaces.Application.DTOs.ClientView;
using Partnr.Workspaces.Application.Exceptions;
using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Application.Mappings;
using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;

namespace Partnr.Workspaces.Application.Services;

internal sealed class ApprovalService(
    IApprovalRequestRepository approvals,
    IDeliverableRepository deliverables,
    ProjectAccess projectAccess,
    DeliverableResponseBuilder responses,
    INotificationPublisher notifications,
    WorkspaceAccess access,
    ClientAccess clientAccess,
    CallerAccess callers,
    IUnitOfWork unitOfWork) : IApprovalService
{
    public async Task<DeliverableResponse> RequestAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        RequestApprovalRequest request,
        CancellationToken cancellationToken)
    {
        var membership = await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await projectAccess.RequireEditableProjectAsync(workspaceId, projectId, cancellationToken);
        var deliverable = await RequireDeliverableAsync(workspaceId, projectId, deliverableId, cancellationToken);

        ProjectContentRules.EnsureNotArchived(deliverable.IsArchived, "deliverable");

        if (deliverable.Status != DeliverableStatus.Shared)
        {
            throw new ConflictException("Share the deliverable with the client before asking for their approval.");
        }

        if (await approvals.GetPendingAsync(deliverable.Id, cancellationToken) is not null)
        {
            throw new ConflictException("This deliverable already has an approval request waiting for the client.");
        }

        var round = ApprovalRequest.Request(workspaceId, projectId, deliverable.Id, membership.UserId, request.Note);
        approvals.Add(round);
        await notifications.ApprovalRequestedAsync(membership.Workspace.Name, project, deliverable, round, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(deliverable, project, cancellationToken);
    }

    public async Task<DeliverableResponse> WithdrawAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await projectAccess.RequireEditableProjectAsync(workspaceId, projectId, cancellationToken);
        var deliverable = await RequireDeliverableAsync(workspaceId, projectId, deliverableId, cancellationToken);
        var approval = await approvals.GetByIdAsync(workspaceId, projectId, deliverableId, approvalId, cancellationToken)
            ?? throw new NotFoundException($"The approval request with id '{approvalId}' was not found.");

        if (!approval.IsPending)
        {
            throw new ConflictException("This approval request is already closed.");
        }

        approval.Withdraw();
        await notifications.ApprovalRequestWithdrawnAsync(approval.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(deliverable, project, cancellationToken);
    }

    public Task<ClientDeliverableResponse> ApproveAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        ApproveRequest request,
        CancellationToken cancellationToken) =>
        DecideAsync(workspaceId, projectId, deliverableId, approvalId, (round, userId) => round.Approve(userId, request.Message), cancellationToken);

    public Task<ClientDeliverableResponse> RequestChangesAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        RequestChangesRequest request,
        CancellationToken cancellationToken) =>
        DecideAsync(workspaceId, projectId, deliverableId, approvalId, (round, userId) => round.RequestChanges(userId, request.Message), cancellationToken);

    private async Task<ClientDeliverableResponse> DecideAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        Action<ApprovalRequest, Guid> decide,
        CancellationToken cancellationToken)
    {
        // Only the client decides: a member of the firm asking for it is told the project is not there, like anyone else without the door.
        var caller = await callers.RequireAsync(workspaceId, cancellationToken);
        var project = caller.IsClient
            ? await clientAccess.FindProjectInWorkspaceAsync(caller.UserId, workspaceId, projectId, cancellationToken)
            : null;
        if (project is null)
        {
            throw new NotFoundException($"The project with id '{projectId}' was not found.");
        }

        var deliverable = await deliverables.GetByIdAsync(project.WorkspaceId, project.Id, deliverableId, cancellationToken);
        if (deliverable is null || deliverable.Status != DeliverableStatus.Shared || deliverable.IsArchived)
        {
            throw new NotFoundException($"The deliverable with id '{deliverableId}' was not found.");
        }

        var round = await approvals.GetByIdAsync(project.WorkspaceId, project.Id, deliverable.Id, approvalId, cancellationToken)
            ?? throw new NotFoundException($"The approval request with id '{approvalId}' was not found.");

        if (!round.IsPending)
        {
            throw new ConflictException("This request is no longer open.");
        }

        decide(round, caller.UserId);
        await notifications.ApprovalDecidedAsync(project, deliverable, round, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var stageName = deliverable.StageId is { } stageId ? project.Stages.FirstOrDefault(s => s.Id == stageId)?.Name : null;
        var rounds = await approvals.ListForDeliverableAsync(project.WorkspaceId, project.Id, deliverable.Id, cancellationToken);

        return deliverable.ToClientResponse(stageName, rounds);
    }

    private async Task<Deliverable> RequireDeliverableAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        CancellationToken cancellationToken) =>
        await deliverables.GetByIdAsync(workspaceId, projectId, deliverableId, cancellationToken)
            ?? throw new NotFoundException($"The deliverable with id '{deliverableId}' was not found.");
}
