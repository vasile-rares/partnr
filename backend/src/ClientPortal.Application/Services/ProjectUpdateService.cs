using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.ProjectUpdates;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Application.Mappings;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.Application.Services;

internal sealed class ProjectUpdateService(
    IProjectUpdateRepository updates,
    IProjectRepository projects,
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
        var project = await RequireProjectAsync(workspaceId, projectId, cancellationToken);
        EnsureProjectNotArchived(project);

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
        await RequireProjectAsync(workspaceId, projectId, cancellationToken);

        var list = await updates.ListAsync(workspaceId, projectId, includeArchived, cancellationToken);

        return list.Select(u => u.ToResponse()).ToList();
    }

    public async Task<ProjectUpdateResponse> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        await RequireProjectAsync(workspaceId, projectId, cancellationToken);
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
        EnsureNotArchived(update);

        if (update.Status != ProjectUpdateStatus.Draft)
        {
            throw new ConflictException("Only a draft can be deleted. Move it back to Draft, or archive it.");
        }

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
        EnsureNotArchived(update);

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
        var project = await RequireProjectAsync(workspaceId, projectId, cancellationToken);
        EnsureProjectNotArchived(project);

        return await RequireUpdateAsync(workspaceId, projectId, id, cancellationToken);
    }

    private async Task<Project> RequireProjectAsync(Guid workspaceId, Guid projectId, CancellationToken cancellationToken) =>
        await projects.GetByIdAsync(workspaceId, projectId, cancellationToken)
            ?? throw new NotFoundException($"The project with id '{projectId}' was not found.");

    private async Task<ProjectUpdate> RequireUpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        CancellationToken cancellationToken) =>
        await updates.GetByIdAsync(workspaceId, projectId, id, cancellationToken)
            ?? throw new NotFoundException($"The update with id '{id}' was not found.");

    private static void EnsureProjectNotArchived(Project project)
    {
        if (project.IsArchived)
        {
            throw new ConflictException("This project is archived. Restore it before making changes.");
        }
    }

    private static void EnsureNotArchived(ProjectUpdate update)
    {
        if (update.IsArchived)
        {
            throw new ConflictException("This update is archived. Restore it before making changes.");
        }
    }
}
