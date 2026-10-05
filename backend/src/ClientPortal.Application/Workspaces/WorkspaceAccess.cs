using ClientPortal.Application.Common;
using ClientPortal.Application.Common.Exceptions;
using ClientPortal.Domain.Workspaces;

namespace ClientPortal.Application.Workspaces;

internal sealed record WorkspaceMembership(Workspace Workspace, Guid UserId, WorkspaceRole Role);

/// <summary>
/// The one place that decides who may touch a workspace. Every service that reads or changes data inside a workspace
/// must go through here first, so the access rule cannot be forgotten or implemented twice.
/// </summary>
internal sealed class WorkspaceAccess(IWorkspaceRepository workspaces, ICurrentUser currentUser)
{
    public Guid RequireUserId() => currentUser.UserId ?? throw new UnauthorizedException("Not signed in.");

    /// <exception cref="NotFoundException">The workspace does not exist or the user is not a member (deliberately the same answer).</exception>
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

    /// <exception cref="NotFoundException">As <see cref="RequireMemberAsync"/>.</exception>
    /// <exception cref="ForbiddenException">The user is a member but not the owner.</exception>
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
