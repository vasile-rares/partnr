using Partnr.Core.Application.Authorization;
using Partnr.Core.Application.DTOs.Members;
using Partnr.Core.Application.Exceptions;
using Partnr.Core.Application.Interfaces.Repositories;
using Partnr.Core.Application.Interfaces.Services;
using Partnr.Core.Domain.Enums;

namespace Partnr.Core.Application.Services;

internal sealed class MemberService(
    IIdentityService identity,
    IMeetingRepository meetings,
    WorkspaceAccess access,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IMemberService
{
    public async Task<IReadOnlyList<MemberResponse>> ListAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var membership = await access.RequireMemberAsync(workspaceId, cancellationToken);
        var members = membership.Workspace.Members;
        var accounts = (await identity.FindByIdsAsync(members.Select(m => m.UserId).ToList(), cancellationToken)).ToDictionary(a => a.Id);

        return members
            .OrderBy(m => m.Role == WorkspaceRole.Owner ? 0 : 1)
            .ThenBy(m => m.JoinedAtUtc)
            .Select(m =>
            {
                accounts.TryGetValue(m.UserId, out var account);
                var email = account?.Email ?? string.Empty;
                var name = string.IsNullOrWhiteSpace(account?.FullName) ? email : account!.FullName;

                return new MemberResponse(m.UserId, name, email, m.Role.ToString(), m.JoinedAtUtc);
            })
            .ToList();
    }

    public async Task RemoveAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken)
    {
        var membership = await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var role = membership.Workspace.RoleOf(userId)
            ?? throw new NotFoundException($"The member with id '{userId}' was not found.");

        if (role == WorkspaceRole.Owner)
        {
            throw new ConflictException("The owner of a workspace cannot be removed.");
        }

        // A meeting's Google event lives in its organiser's calendar and follows every change: leaving it behind would keep
        // sending someone who is gone the details of a client they can no longer see.
        var upcoming = await meetings.CountUpcomingForOrganizerAsync(workspaceId, userId, time.GetUtcNow().UtcDateTime, cancellationToken);
        if (upcoming > 0)
        {
            var account = await identity.FindByIdAsync(userId, cancellationToken);
            var who = string.IsNullOrWhiteSpace(account?.FullName) ? account?.Email ?? "This member" : account!.FullName;
            var count = upcoming == 1 ? "1 upcoming meeting" : $"{upcoming} upcoming meetings";

            throw new ConflictException($"{who} still organises {count}. Cancel {(upcoming == 1 ? "it" : "them")} before removing this person.");
        }

        membership.Workspace.RemoveMember(userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
