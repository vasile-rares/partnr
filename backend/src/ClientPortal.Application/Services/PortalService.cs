using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Portal;
using ClientPortal.Application.DTOs.Projects;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Services;

internal sealed class PortalService(
    IWorkspaceRepository workspaces,
    IProjectRepository projects,
    IDeliverableRepository deliverables,
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

        // A project always has its workspace here (the client is linked there); skipping is only a guard against a race.
        return list
            .Where(p => names.ContainsKey(p.WorkspaceId))
            .Select(p => new PortalProjectSummaryResponse(
                p.Id,
                names[p.WorkspaceId],
                p.Name,
                p.Status.ToString(),
                p.DueDate,
                Progress(p)))
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
            shared
                .Select(d => new PortalDeliverableResponse(
                    d.Id,
                    d.Title,
                    d.Description,
                    d.Url,
                    d.StageId is { } stageId ? stageNames.GetValueOrDefault(stageId) : null))
                .ToList());
    }

    private async Task<Dictionary<Guid, PortalWorkspaceResponse>> WorkspacesAsync(Guid userId, CancellationToken cancellationToken) =>
        (await workspaces.ListForClientAsync(userId, cancellationToken))
            .ToDictionary(w => w.Id, w => new PortalWorkspaceResponse(w.Id, w.Name));

    private static ProjectProgressResponse Progress(Project project) => new(project.DoneStageCount, project.Stages.Count);
}
