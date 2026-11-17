using Partnr.Application.Authorization;
using Partnr.Application.DTOs.ProjectFiles;
using Partnr.Application.Exceptions;
using Partnr.Application.Interfaces.Repositories;
using Partnr.Application.Interfaces.Services;
using Partnr.Application.Mappings;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;

namespace Partnr.Application.Services;

internal sealed class ProjectFileService(
    IProjectFileRepository files,
    ProjectAccess projectAccess,
    IFileStorage storage,
    WorkspaceAccess access,
    ClientAccess clientAccess,
    CallerAccess callers,
    IUnitOfWork unitOfWork) : IProjectFileService
{
    public async Task<ProjectFileResponse> CreateAsync(
        Guid workspaceId,
        Guid projectId,
        UploadProjectFileRequest request,
        FileUpload upload,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        await projectAccess.RequireEditableProjectAsync(workspaceId, projectId, cancellationToken);

        var file = ProjectFile.Create(workspaceId, projectId, request.Name, request.Description, upload.FileName, upload.Length);

        // Bytes first: a row that points at nothing is worse than an object no row points at.
        await storage.SaveAsync(file.StorageKey, upload.Content, file.ContentType, cancellationToken);
        files.Add(file);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(file.StorageKey, CancellationToken.None);
            throw;
        }

        return file.ToResponse();
    }

    public async Task<IReadOnlyList<ProjectFileResponse>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        await projectAccess.RequireProjectAsync(workspaceId, projectId, cancellationToken);

        var list = await files.ListAsync(workspaceId, projectId, includeArchived, cancellationToken);

        return list.Select(f => f.ToResponse()).ToList();
    }

    public async Task<ProjectFileResponse> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        await projectAccess.RequireProjectAsync(workspaceId, projectId, cancellationToken);
        var file = await RequireFileAsync(workspaceId, projectId, id, cancellationToken);

        return file.ToResponse();
    }

    public async Task<FileDownload> DownloadAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        var caller = await callers.RequireAsync(workspaceId, cancellationToken);
        if (caller.IsClient)
        {
            // Through the same door as reading the project, and only a file that was shared and is not archived.
            _ = await clientAccess.FindProjectInWorkspaceAsync(caller.UserId, workspaceId, projectId, cancellationToken)
                ?? throw new NotFoundException($"The project with id '{projectId}' was not found.");
            var shared = await files.GetByIdAsync(workspaceId, projectId, id, cancellationToken);
            if (shared is null || shared.Status != ProjectFileStatus.Shared || shared.IsArchived)
            {
                throw new NotFoundException($"The file with id '{id}' was not found.");
            }

            return await OpenAsync(shared, cancellationToken);
        }

        await projectAccess.RequireProjectAsync(workspaceId, projectId, cancellationToken);

        return await OpenAsync(await RequireFileAsync(workspaceId, projectId, id, cancellationToken), cancellationToken);
    }

    public Task<ProjectFileResponse> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        SaveProjectFileRequest request,
        CancellationToken cancellationToken) =>
        ChangeAsync(workspaceId, projectId, id, file => file.UpdateDetails(request.Name, request.Description), cancellationToken);

    public Task<ProjectFileResponse> ShareAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(workspaceId, projectId, id, file => file.Share(), cancellationToken);

    public Task<ProjectFileResponse> UnshareAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(workspaceId, projectId, id, file => file.Unshare(), cancellationToken);

    public async Task<ProjectFileResponse> ArchiveAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var file = await RequireEditableProjectAndFileAsync(workspaceId, projectId, id, cancellationToken);

        file.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return file.ToResponse();
    }

    public async Task<ProjectFileResponse> RestoreAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var file = await RequireEditableProjectAndFileAsync(workspaceId, projectId, id, cancellationToken);

        file.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return file.ToResponse();
    }

    public async Task DeleteAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var file = await RequireEditableProjectAndFileAsync(workspaceId, projectId, id, cancellationToken);
        ProjectContentRules.EnsureNotArchived(file.IsArchived, "file");

        ProjectContentRules.EnsureDeletable(file.Status == ProjectFileStatus.Draft);

        files.Remove(file);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // After the commit: if this fails the file is only an orphaned object, never a row pointing at nothing.
        await storage.DeleteAsync(file.StorageKey, CancellationToken.None);
    }

    private async Task<FileDownload> OpenAsync(ProjectFile file, CancellationToken cancellationToken) =>
        new(file.FileName, file.ContentType, file.SizeBytes, await storage.OpenReadAsync(file.StorageKey, cancellationToken));

    // Every member-level change has the same shape: find both, refuse if either is archived, change, save once.
    private async Task<ProjectFileResponse> ChangeAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        Action<ProjectFile> change,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var file = await RequireEditableProjectAndFileAsync(workspaceId, projectId, id, cancellationToken);
        ProjectContentRules.EnsureNotArchived(file.IsArchived, "file");

        change(file);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return file.ToResponse();
    }

    private async Task<ProjectFile> RequireEditableProjectAndFileAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await projectAccess.RequireEditableProjectAsync(workspaceId, projectId, cancellationToken);

        return await RequireFileAsync(workspaceId, projectId, id, cancellationToken);
    }

    private async Task<ProjectFile> RequireFileAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        await files.GetByIdAsync(workspaceId, projectId, id, cancellationToken)
            ?? throw new NotFoundException($"The file with id '{id}' was not found.");
}
