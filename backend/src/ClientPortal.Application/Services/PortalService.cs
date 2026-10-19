using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Portal;
using ClientPortal.Application.DTOs.ProjectFiles;
using ClientPortal.Application.DTOs.Projects;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Application.Mappings;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.Application.Services;

internal sealed class PortalService(
    IWorkspaceRepository workspaces,
    IProjectRepository projects,
    IDeliverableRepository deliverables,
    IProjectUpdateRepository projectUpdates,
    IProjectFileRepository projectFiles,
    IApprovalRequestRepository approvals,
    IFileStorage storage,
    WorkspaceAccess access) : IPortalService
{
    public async Task<IReadOnlyList<PortalWorkspaceResponse>> ListWorkspacesAsync(CancellationToken cancellationToken)
    {
        var list = await workspaces.ListForClientAsync(access.RequireUserId(), cancellationToken);

        return list.Select(w => new PortalWorkspaceResponse(w.Id, w.Name)).ToList();
    }

    public async Task<IReadOnlyList<PortalProjectSummaryResponse>> ListProjectsAsync(CancellationToken cancellationToken)
    {
        var userId = access.RequireUserId();
        var list = await projects.ListSharedWithClientAsync(userId, cancellationToken);
        var names = await WorkspacesAsync(userId, cancellationToken);
        var waiting = await approvals.CountPendingForClientAsync(list.Select(p => p.Id).ToList(), cancellationToken);

        // A project always has its workspace here (the client is linked there); skipping is only a guard against a race.
        return list
            .Where(p => names.ContainsKey(p.WorkspaceId))
            .Select(p => new PortalProjectSummaryResponse(
                p.Id,
                names[p.WorkspaceId],
                p.Name,
                p.Status.ToString(),
                p.DueDate,
                Progress(p),
                waiting.GetValueOrDefault(p.Id)))
            .ToList();
    }

    public async Task<PortalProjectResponse> GetProjectAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = access.RequireUserId();

        var project = await projects.GetSharedWithClientAsync(userId, id, cancellationToken);
        var names = await WorkspacesAsync(userId, cancellationToken);
        if (project is null || !names.TryGetValue(project.WorkspaceId, out var workspace))
        {
            throw new NotFoundException($"The project with id '{id}' was not found.");
        }

        var stages = project.Stages;
        var stageNames = stages.ToDictionary(s => s.Id, s => s.Name);
        var shared = await deliverables.ListSharedAsync(project.Id, cancellationToken);
        var sharedUpdates = await projectUpdates.ListSharedAsync(project.Id, cancellationToken);
        var sharedFiles = await projectFiles.ListSharedAsync(project.Id, cancellationToken);
        var rounds = (await approvals.ListForProjectAsync(project.WorkspaceId, project.Id, cancellationToken)).ToLookup(a => a.DeliverableId);
        var sharedDeliverables = shared
            .Select(d => d.ToPortalResponse(d.StageId is { } stageId ? stageNames.GetValueOrDefault(stageId) : null, rounds[d.Id]))
            .ToList();

        return new PortalProjectResponse(
            project.Id,
            workspace,
            project.Name,
            project.Description,
            project.StartDate,
            project.DueDate,
            project.Status.ToString(),
            Progress(project),
            stages.Select(s => new StageResponse(s.Id, s.Name, s.Description, s.DueDate, s.Status.ToString())).ToList(),
            sharedDeliverables,
            // Share() always sets the date, so a shared update has one.
            sharedUpdates.Select(u => new PortalProjectUpdateResponse(u.Id, u.Title, u.Body, u.SharedAtUtc!.Value)).ToList(),
            sharedFiles.Select(f => f.ToPortalResponse()).ToList(),
            sharedDeliverables.Count(d => d.Approval.Pending is not null));
    }

    public async Task<FileDownload> DownloadFileAsync(Guid projectId, Guid fileId, CancellationToken cancellationToken)
    {
        var userId = access.RequireUserId();

        var project = await projects.GetSharedWithClientAsync(userId, projectId, cancellationToken)
            ?? throw new NotFoundException($"The project with id '{projectId}' was not found.");

        var file = await projectFiles.GetByIdAsync(project.WorkspaceId, project.Id, fileId, cancellationToken);
        if (file is null || file.Status != ProjectFileStatus.Shared || file.IsArchived)
        {
            throw new NotFoundException($"The file with id '{fileId}' was not found.");
        }

        var content = await storage.OpenReadAsync(file.StorageKey, cancellationToken);

        return new FileDownload(file.FileName, file.ContentType, file.SizeBytes, content);
    }

    private async Task<Dictionary<Guid, PortalWorkspaceResponse>> WorkspacesAsync(Guid userId, CancellationToken cancellationToken) =>
        (await workspaces.ListForClientAsync(userId, cancellationToken))
            .ToDictionary(w => w.Id, w => new PortalWorkspaceResponse(w.Id, w.Name));

    private static ProjectProgressResponse Progress(Project project) => new(project.DoneStageCount, project.Stages.Count);
}
