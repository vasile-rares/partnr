using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;

namespace Partnr.Workspaces.Application.Authorization;

/// <summary>Who is calling a workspace's route and in what capacity: a member (with a role) or a client of it (no role).</summary>
internal sealed record Caller(Guid UserId, Workspace Workspace, WorkspaceRole? Role)
{
    public bool IsClient => Role is null;
}

/// <summary>
/// For routes that are open to both sides. A route that is not should call <see cref="WorkspaceAccess.RequireMemberAsync"/>
/// (or <see cref="ClientAccess"/>) instead: that is how it stays closed to the other side by default.
/// </summary>
internal sealed class CallerAccess(WorkspaceAccess members, ClientAccess clients)
{
    // An account is never both (accepting an invitation refuses a member), and one that is neither gets the usual 404.
    public async Task<Caller> RequireAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = members.RequireUserId();
        var membership = await members.FindMemberAsync(workspaceId, cancellationToken);
        if (membership is not null)
        {
            return new Caller(userId, membership.Workspace, membership.Role);
        }

        var workspace = await clients.RequireWorkspaceAsync(userId, workspaceId, cancellationToken);

        return new Caller(userId, workspace, null);
    }
}
