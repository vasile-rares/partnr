using Partnr.Application.Exceptions;
using Partnr.Application.Interfaces.Repositories;
using Partnr.Application.Interfaces.Services;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;

namespace Partnr.Application.Authorization;

internal sealed record WorkspaceMembership(Workspace Workspace, Guid UserId, WorkspaceRole Role);

/// <summary>The one place that decides who may touch a workspace.</summary>
internal sealed class WorkspaceAccess(IWorkspaceRepository workspaces, ICurrentUser currentUser)
{
    public Guid RequireUserId() => currentUser.UserId ?? throw new UnauthorizedException("Not signed in.");

    /// <summary>Null when the workspace does not exist or the caller is not a member of it.</summary>
    public async Task<WorkspaceMembership?> FindMemberAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var workspace = await workspaces.GetByIdAsync(workspaceId, cancellationToken);
        var role = workspace?.RoleOf(userId);

        return workspace is null || role is null ? null : new WorkspaceMembership(workspace, userId, role.Value);
    }

    // Same answer for "does not exist" and "not yours", so ids cannot be probed.
    public async Task<WorkspaceMembership> RequireMemberAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        await FindMemberAsync(workspaceId, cancellationToken)
            ?? throw new NotFoundException($"The workspace with id '{workspaceId}' was not found.");

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
