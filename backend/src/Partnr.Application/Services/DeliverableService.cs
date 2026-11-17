using Partnr.Application.Authorization;
using Partnr.Application.DTOs.Deliverables;
using Partnr.Application.Exceptions;
using Partnr.Application.Interfaces.Repositories;
using Partnr.Application.Interfaces.Services;
using Partnr.Application.Mappings;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;

namespace Partnr.Application.Services;

internal sealed class DeliverableService(
    IDeliverableRepository deliverables,
    ProjectAccess projectAccess,
    IApprovalRequestRepository approvals,
    DeliverableResponseBuilder responses,
    INotificationWriter notifications,
    WorkspaceAccess access,
    IUnitOfWork unitOfWork) : IDeliverableService
{
    public async Task<DeliverableResponse> CreateAsync(
        Guid workspaceId,
        Guid projectId,
        SaveDeliverableRequest request,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await projectAccess.RequireEditableProjectAsync(workspaceId, projectId, cancellationToken);
        RequireStage(project, request.StageId);

        var deliverable = Deliverable.Create(workspaceId, projectId, request.StageId, request.Title, request.Description, request.Url);
        deliverables.Add(deliverable);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return deliverable.ToResponse(project, []);
    }

    public async Task<IReadOnlyList<DeliverableResponse>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await projectAccess.RequireProjectAsync(workspaceId, projectId, cancellationToken);

        var list = await deliverables.ListAsync(workspaceId, projectId, includeArchived, cancellationToken);

        return await responses.BuildAsync(list, project, cancellationToken);
    }

    public async Task<DeliverableResponse> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await projectAccess.RequireProjectAsync(workspaceId, projectId, cancellationToken);
        var deliverable = await RequireDeliverableAsync(workspaceId, projectId, id, cancellationToken);

        return await responses.BuildAsync(deliverable, project, cancellationToken);
    }

    public Task<DeliverableResponse> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        SaveDeliverableRequest request,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            workspaceId,
            projectId,
            id,
            async (deliverable, project) =>
            {
                RequireStage(project, request.StageId);

                // The client must not approve something other than what they saw, so an open request freezes the details.
                if (await approvals.GetPendingAsync(deliverable.Id, cancellationToken) is not null)
                {
                    throw new ConflictException("This deliverable is waiting for the client's decision. Withdraw the approval request before editing it.");
                }

                deliverable.UpdateDetails(request.StageId, request.Title, request.Description, request.Url);
            },
            cancellationToken);

    public Task<DeliverableResponse> ShareAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(workspaceId, projectId, id, (deliverable, _) => { deliverable.Share(); return Task.CompletedTask; }, cancellationToken);

    public Task<DeliverableResponse> UnshareAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(
            workspaceId,
            projectId,
            id,
            async (deliverable, _) =>
            {
                await WithdrawPendingAsync(deliverable, cancellationToken);
                deliverable.Unshare();
            },
            cancellationToken);

    public async Task<DeliverableResponse> ArchiveAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var project = await projectAccess.RequireEditableProjectAsync(workspaceId, projectId, cancellationToken);
        var deliverable = await RequireDeliverableAsync(workspaceId, projectId, id, cancellationToken);

        await WithdrawPendingAsync(deliverable, cancellationToken);
        deliverable.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(deliverable, project, cancellationToken);
    }

    public async Task<DeliverableResponse> RestoreAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var project = await projectAccess.RequireEditableProjectAsync(workspaceId, projectId, cancellationToken);
        var deliverable = await RequireDeliverableAsync(workspaceId, projectId, id, cancellationToken);

        deliverable.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(deliverable, project, cancellationToken);
    }

    public async Task DeleteAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await projectAccess.RequireEditableProjectAsync(workspaceId, projectId, cancellationToken);
        var deliverable = await RequireDeliverableAsync(workspaceId, projectId, id, cancellationToken);
        ProjectContentRules.EnsureNotArchived(deliverable.IsArchived, "deliverable");

        ProjectContentRules.EnsureDeletable(deliverable.Status == DeliverableStatus.Draft);

        if (await approvals.AnyAsync(deliverable.Id, cancellationToken))
        {
            throw new ConflictException("This deliverable has an approval history, so it can't be deleted. Archive it instead.");
        }

        deliverables.Remove(deliverable);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Every member-level change has the same shape: find both, refuse if either is archived, change, save once.
    private async Task<DeliverableResponse> ChangeAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        Func<Deliverable, Project, Task> change,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await projectAccess.RequireEditableProjectAsync(workspaceId, projectId, cancellationToken);
        var deliverable = await RequireDeliverableAsync(workspaceId, projectId, id, cancellationToken);
        ProjectContentRules.EnsureNotArchived(deliverable.IsArchived, "deliverable");

        await change(deliverable, project);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(deliverable, project, cancellationToken);
    }

    // The client stops seeing the deliverable, so a request waiting for them cannot stay open.
    private async Task WithdrawPendingAsync(Deliverable deliverable, CancellationToken cancellationToken)
    {
        var pending = await approvals.GetPendingAsync(deliverable.Id, cancellationToken);
        if (pending is null)
        {
            return;
        }

        pending.Withdraw();
        await notifications.ApprovalRequestWithdrawnAsync(pending.Id, cancellationToken);
    }

    private async Task<Deliverable> RequireDeliverableAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        CancellationToken cancellationToken) =>
        await deliverables.GetByIdAsync(workspaceId, projectId, id, cancellationToken)
            ?? throw new NotFoundException($"The deliverable with id '{id}' was not found.");

    /// <summary>A stage of another project is a 404, like an unknown one.</summary>
    private static void RequireStage(Project project, Guid? stageId)
    {
        if (stageId is { } id && !project.HasStage(id))
        {
            throw new NotFoundException($"The stage with id '{id}' was not found.");
        }
    }
}
