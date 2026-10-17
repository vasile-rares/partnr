using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.Application.Authorization;

internal sealed record WorkspaceMembership(Workspace Workspace, Guid UserId, WorkspaceRole Role);

/// <summary>The one place that decides who may touch a workspace.</summary>
internal sealed class WorkspaceAccess(IWorkspaceRepository workspaces, ICurrentUser currentUser)
{
    public Guid RequireUserId() => currentUser.UserId ?? throw new UnauthorizedException("Not signed in.");

    public async Task<WorkspaceMembership> RequireMemberAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var workspace = await workspaces.GetByIdAsync(workspaceId, cancellationToken);

        // Same answer for "does not exist" and "not yours", so ids cannot be probed.
        var role = workspace?.RoleOf(userId);
        if (workspace is null || role is null)
        {
            throw new NotFoundException($"The workspace with id '{workspaceId}' was not found.");
        }

        return new WorkspaceMembership(workspace, userId, role.Value);
    }

    public async Task<WorkspaceMembership> RequireOwnerAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var membership = await RequireMemberAsync(workspaceId, cancellationToken);
        if (membership.Role != WorkspaceRole.Owner)
        {
            throw new ForbiddenException("Only the workspace owner can do this.");
        }

        return membership;
    }
}
