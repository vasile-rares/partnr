using Partnr.Workspaces.Application.Exceptions;
using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Domain.Entities;

namespace Partnr.Workspaces.Application.Authorization;

/// <summary>The one place that decides what a client account may open: the client's counterpart of <see cref="WorkspaceAccess"/>.</summary>
internal sealed class ClientAccess(IProjectRepository projects, IWorkspaceRepository workspaces)
{
    /// <summary>Null when the project is unknown, not shared yet, archived or someone else's.</summary>
    public Task<Project?> FindProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        projects.GetSharedWithClientAsync(userId, projectId, cancellationToken);

    /// <summary>Like <see cref="FindProjectAsync"/>, and the project must belong to this firm: another firm's is not found either.</summary>
    public async Task<Project?> FindProjectInWorkspaceAsync(Guid userId, Guid workspaceId, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await FindProjectAsync(userId, projectId, cancellationToken);

        return project is not null && project.WorkspaceId == workspaceId ? project : null;
    }

    // Those cases answer the same, so ids cannot be probed.
    public async Task<Project> RequireProjectAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        await FindProjectAsync(userId, projectId, cancellationToken)
            ?? throw new NotFoundException($"The project with id '{projectId}' was not found.");

    public async Task<Workspace> RequireWorkspaceAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken) =>
        (await workspaces.ListForClientAsync(userId, cancellationToken)).FirstOrDefault(w => w.Id == workspaceId)
            ?? throw new NotFoundException($"The workspace with id '{workspaceId}' was not found.");

    public Task<IReadOnlyList<Workspace>> ListWorkspacesAsync(Guid userId, CancellationToken cancellationToken) =>
        workspaces.ListForClientAsync(userId, cancellationToken);

    /// <summary>The projects shared with this account, all firms or only one.</summary>
    public async Task<IReadOnlyList<Project>> ListProjectsAsync(Guid userId, Guid? workspaceId, CancellationToken cancellationToken)
    {
        var list = await projects.ListSharedWithClientAsync(userId, cancellationToken);

        return workspaceId is { } id ? list.Where(p => p.WorkspaceId == id).ToList() : list;
    }
}
