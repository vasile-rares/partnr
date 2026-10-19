using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.ProjectFiles;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Application.Mappings;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.Application.Services;

internal sealed class ProjectFileService(
    IProjectFileRepository files,
    IProjectRepository projects,
    IFileStorage storage,
    WorkspaceAccess access,
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
        var project = await RequireProjectAsync(workspaceId, projectId, cancellationToken);
        EnsureProjectNotArchived(project);

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
        await RequireProjectAsync(workspaceId, projectId, cancellationToken);

        var list = await files.ListAsync(workspaceId, projectId, includeArchived, cancellationToken);

        return list.Select(f => f.ToResponse()).ToList();
    }

    public async Task<ProjectFileResponse> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        await RequireProjectAsync(workspaceId, projectId, cancellationToken);
        var file = await RequireFileAsync(workspaceId, projectId, id, cancellationToken);

        return file.ToResponse();
    }

    public async Task<FileDownload> DownloadAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        await RequireProjectAsync(workspaceId, projectId, cancellationToken);
        var file = await RequireFileAsync(workspaceId, projectId, id, cancellationToken);

        var content = await storage.OpenReadAsync(file.StorageKey, cancellationToken);

        return new FileDownload(file.FileName, file.ContentType, file.SizeBytes, content);
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
        EnsureNotArchived(file);

        if (file.Status != ProjectFileStatus.Draft)
        {
            throw new ConflictException("Only a draft can be deleted. Move it back to Draft, or archive it.");
        }

        files.Remove(file);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // After the commit: if this fails the file is only an orphaned object, never a row pointing at nothing.
        await storage.DeleteAsync(file.StorageKey, CancellationToken.None);
    }

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
        EnsureNotArchived(file);

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
        var project = await RequireProjectAsync(workspaceId, projectId, cancellationToken);
        EnsureProjectNotArchived(project);

        return await RequireFileAsync(workspaceId, projectId, id, cancellationToken);
    }

    private async Task<Project> RequireProjectAsync(Guid workspaceId, Guid projectId, CancellationToken cancellationToken) =>
        await projects.GetByIdAsync(workspaceId, projectId, cancellationToken)
            ?? throw new NotFoundException($"The project with id '{projectId}' was not found.");

    private async Task<ProjectFile> RequireFileAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        await files.GetByIdAsync(workspaceId, projectId, id, cancellationToken)
            ?? throw new NotFoundException($"The file with id '{id}' was not found.");

    private static void EnsureProjectNotArchived(Project project)
    {
        if (project.IsArchived)
        {
            throw new ConflictException("This project is archived. Restore it before making changes.");
        }
    }

    private static void EnsureNotArchived(ProjectFile file)
    {
        if (file.IsArchived)
        {
            throw new ConflictException("This file is archived. Restore it before making changes.");
        }
    }
}
