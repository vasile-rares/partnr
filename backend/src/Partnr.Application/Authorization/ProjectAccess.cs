using Partnr.Application.Exceptions;
using Partnr.Application.Interfaces.Repositories;
using Partnr.Domain.Entities;

namespace Partnr.Application.Authorization;

/// <summary>The one place that finds a project of a workspace for the firm's side, and refuses changes once it is archived.</summary>
internal sealed class ProjectAccess(IProjectRepository projects)
{
    public async Task<Project> RequireProjectAsync(Guid workspaceId, Guid projectId, CancellationToken cancellationToken) =>
        await projects.GetByIdAsync(workspaceId, projectId, cancellationToken)
            ?? throw new NotFoundException($"The project with id '{projectId}' was not found.");

    /// <summary>For anything that changes the project or what it holds: an archived project is read-only until restored.</summary>
    public async Task<Project> RequireEditableProjectAsync(Guid workspaceId, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await RequireProjectAsync(workspaceId, projectId, cancellationToken);
        if (project.IsArchived)
        {
            throw new ConflictException("This project is archived. Restore it before making changes.");
        }

        return project;
    }
}
