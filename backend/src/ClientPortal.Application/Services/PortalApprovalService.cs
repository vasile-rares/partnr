using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Approvals;
using ClientPortal.Application.DTOs.Portal;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Application.Mappings;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.Application.Services;

internal sealed class PortalApprovalService(
    IProjectRepository projects,
    IDeliverableRepository deliverables,
    IApprovalRequestRepository approvals,
    WorkspaceAccess access,
    IUnitOfWork unitOfWork) : IPortalApprovalService
{
    public Task<PortalDeliverableResponse> ApproveAsync(
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        ApproveRequest request,
        CancellationToken cancellationToken) =>
        DecideAsync(projectId, deliverableId, approvalId, (round, userId) => round.Approve(userId, request.Message), cancellationToken);

    public Task<PortalDeliverableResponse> RequestChangesAsync(
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        RequestChangesRequest request,
        CancellationToken cancellationToken) =>
        DecideAsync(projectId, deliverableId, approvalId, (round, userId) => round.RequestChanges(userId, request.Message), cancellationToken);

    private async Task<PortalDeliverableResponse> DecideAsync(
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        Action<ApprovalRequest, Guid> decide,
        CancellationToken cancellationToken)
    {
        var userId = access.RequireUserId();

        // The same door as reading the project: a draft, archived or someone else's project is simply not there.
        var project = await projects.GetSharedWithClientAsync(userId, projectId, cancellationToken)
            ?? throw new NotFoundException($"The project with id '{projectId}' was not found.");

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

        decide(round, userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var stageName = deliverable.StageId is { } stageId ? project.Stages.FirstOrDefault(s => s.Id == stageId)?.Name : null;
        var rounds = await approvals.ListForDeliverableAsync(project.WorkspaceId, project.Id, deliverable.Id, cancellationToken);

        return deliverable.ToPortalResponse(stageName, rounds);
    }
}
