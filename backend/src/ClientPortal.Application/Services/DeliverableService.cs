using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Deliverables;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Application.Mappings;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.Application.Services;

internal sealed class DeliverableService(
    IDeliverableRepository deliverables,
    IProjectRepository projects,
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
        var project = await RequireProjectAsync(workspaceId, projectId, cancellationToken);
        EnsureProjectNotArchived(project);
        RequireStage(project, request.StageId);

        var deliverable = Deliverable.Create(workspaceId, projectId, request.StageId, request.Title, request.Description, request.Url);
        deliverables.Add(deliverable);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return deliverable.ToResponse(project);
    }

    public async Task<IReadOnlyList<DeliverableResponse>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await RequireProjectAsync(workspaceId, projectId, cancellationToken);

        var list = await deliverables.ListAsync(workspaceId, projectId, includeArchived, cancellationToken);

        return list.ToResponses(project);
    }

    public async Task<DeliverableResponse> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await RequireProjectAsync(workspaceId, projectId, cancellationToken);
        var deliverable = await RequireDeliverableAsync(workspaceId, projectId, id, cancellationToken);

        return deliverable.ToResponse(project);
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
            (deliverable, project) =>
            {
                RequireStage(project, request.StageId);
                deliverable.UpdateDetails(request.StageId, request.Title, request.Description, request.Url);
            },
            cancellationToken);

    public Task<DeliverableResponse> ShareAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(workspaceId, projectId, id, (deliverable, _) => deliverable.Share(), cancellationToken);

    public Task<DeliverableResponse> UnshareAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(workspaceId, projectId, id, (deliverable, _) => deliverable.Unshare(), cancellationToken);

    public async Task<DeliverableResponse> ArchiveAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var project = await RequireProjectAsync(workspaceId, projectId, cancellationToken);
        EnsureProjectNotArchived(project);
        var deliverable = await RequireDeliverableAsync(workspaceId, projectId, id, cancellationToken);

        deliverable.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return deliverable.ToResponse(project);
    }

    public async Task<DeliverableResponse> RestoreAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var project = await RequireProjectAsync(workspaceId, projectId, cancellationToken);
        EnsureProjectNotArchived(project);
        var deliverable = await RequireDeliverableAsync(workspaceId, projectId, id, cancellationToken);

        deliverable.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return deliverable.ToResponse(project);
    }

    public async Task DeleteAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await RequireProjectAsync(workspaceId, projectId, cancellationToken);
        EnsureProjectNotArchived(project);
        var deliverable = await RequireDeliverableAsync(workspaceId, projectId, id, cancellationToken);
        EnsureNotArchived(deliverable);

        if (deliverable.Status != DeliverableStatus.Draft)
        {
            throw new ConflictException("Only a draft can be deleted. Move it back to Draft, or archive it.");
        }

        deliverables.Remove(deliverable);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Every member-level change has the same shape: find both, refuse if either is archived, change, save once.
    private async Task<DeliverableResponse> ChangeAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        Action<Deliverable, Project> change,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await RequireProjectAsync(workspaceId, projectId, cancellationToken);
        EnsureProjectNotArchived(project);
        var deliverable = await RequireDeliverableAsync(workspaceId, projectId, id, cancellationToken);
        EnsureNotArchived(deliverable);

        change(deliverable, project);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return deliverable.ToResponse(project);
    }

    private async Task<Project> RequireProjectAsync(Guid workspaceId, Guid projectId, CancellationToken cancellationToken) =>
        await projects.GetByIdAsync(workspaceId, projectId, cancellationToken)
            ?? throw new NotFoundException($"The project with id '{projectId}' was not found.");

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

    private static void EnsureProjectNotArchived(Project project)
    {
        if (project.IsArchived)
        {
            throw new ConflictException("This project is archived. Restore it before making changes.");
        }
    }

    private static void EnsureNotArchived(Deliverable deliverable)
    {
        if (deliverable.IsArchived)
        {
            throw new ConflictException("This deliverable is archived. Restore it before making changes.");
        }
    }
}
