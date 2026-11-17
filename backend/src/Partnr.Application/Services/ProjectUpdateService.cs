using Partnr.Application.Authorization;
using Partnr.Application.DTOs.ProjectUpdates;
using Partnr.Application.Exceptions;
using Partnr.Application.Interfaces.Repositories;
using Partnr.Application.Interfaces.Services;
using Partnr.Application.Mappings;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;

namespace Partnr.Application.Services;

internal sealed class ProjectUpdateService(
    IProjectUpdateRepository updates,
    ProjectAccess projectAccess,
    WorkspaceAccess access,
    IUnitOfWork unitOfWork) : IProjectUpdateService
{
    public async Task<ProjectUpdateResponse> CreateAsync(
        Guid workspaceId,
        Guid projectId,
        SaveProjectUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        await projectAccess.RequireEditableProjectAsync(workspaceId, projectId, cancellationToken);

        var update = ProjectUpdate.Create(workspaceId, projectId, request.Title, request.Body);
        updates.Add(update);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return update.ToResponse();
    }

    public async Task<IReadOnlyList<ProjectUpdateResponse>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        await projectAccess.RequireProjectAsync(workspaceId, projectId, cancellationToken);

        var list = await updates.ListAsync(workspaceId, projectId, includeArchived, cancellationToken);

        return list.Select(u => u.ToResponse()).ToList();
    }

    public async Task<ProjectUpdateResponse> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        await projectAccess.RequireProjectAsync(workspaceId, projectId, cancellationToken);
        var update = await RequireUpdateAsync(workspaceId, projectId, id, cancellationToken);

        return update.ToResponse();
    }

    public Task<ProjectUpdateResponse> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        SaveProjectUpdateRequest request,
        CancellationToken cancellationToken) =>
        ChangeAsync(workspaceId, projectId, id, update => update.UpdateDetails(request.Title, request.Body), cancellationToken);

    public Task<ProjectUpdateResponse> ShareAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(workspaceId, projectId, id, update => update.Share(), cancellationToken);

    public Task<ProjectUpdateResponse> UnshareAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(workspaceId, projectId, id, update => update.Unshare(), cancellationToken);

    public async Task<ProjectUpdateResponse> ArchiveAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var update = await RequireEditableProjectAndUpdateAsync(workspaceId, projectId, id, cancellationToken);

        update.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return update.ToResponse();
    }

    public async Task<ProjectUpdateResponse> RestoreAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var update = await RequireEditableProjectAndUpdateAsync(workspaceId, projectId, id, cancellationToken);

        update.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return update.ToResponse();
    }

    public async Task DeleteAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var update = await RequireEditableProjectAndUpdateAsync(workspaceId, projectId, id, cancellationToken);
        ProjectContentRules.EnsureNotArchived(update.IsArchived, "update");

        ProjectContentRules.EnsureDeletable(update.Status == ProjectUpdateStatus.Draft);

        updates.Remove(update);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Every member-level change has the same shape: find both, refuse if either is archived, change, save once.
    private async Task<ProjectUpdateResponse> ChangeAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        Action<ProjectUpdate> change,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var update = await RequireEditableProjectAndUpdateAsync(workspaceId, projectId, id, cancellationToken);
        ProjectContentRules.EnsureNotArchived(update.IsArchived, "update");

        change(update);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return update.ToResponse();
    }

    private async Task<ProjectUpdate> RequireEditableProjectAndUpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await projectAccess.RequireEditableProjectAsync(workspaceId, projectId, cancellationToken);

        return await RequireUpdateAsync(workspaceId, projectId, id, cancellationToken);
    }

    private async Task<ProjectUpdate> RequireUpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        CancellationToken cancellationToken) =>
        await updates.GetByIdAsync(workspaceId, projectId, id, cancellationToken)
            ?? throw new NotFoundException($"The update with id '{id}' was not found.");
}
