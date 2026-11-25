using Partnr.Core.Application.Authorization;
using Partnr.Core.Application.DTOs.Members;
using Partnr.Core.Application.Exceptions;
using Partnr.Core.Application.Interfaces.Repositories;
using Partnr.Core.Application.Interfaces.Services;
using Partnr.Core.Application.Mappings;
using Partnr.Core.Domain.Entities;

namespace Partnr.Core.Application.Services;

internal sealed class MemberInvitationService(
    IMemberInvitationRepository invitations,
    IClientRepository clients,
    IIdentityService identity,
    INotificationPublisher notifications,
    WorkspaceAccess access,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IMemberInvitationService
{
    public async Task<MemberInvitationResponse> InviteAsync(Guid workspaceId, InviteMemberRequest request, CancellationToken cancellationToken)
    {
        var membership = await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var email = Client.NormalizeEmail(request.Email);

        // Said now, not when the link is opened: the owner would be sending an email that cannot work.
        var account = await identity.FindByEmailAsync(email, cancellationToken);
        if (account is not null)
        {
            if (membership.Workspace.RoleOf(account.Id) is not null)
            {
                throw new ConflictException("This person is already a member of the workspace.");
            }

            if (await clients.IsLinkedAsync(workspaceId, account.Id, cancellationToken))
            {
                throw new ConflictException("This person is a client of the workspace, so they cannot also be a member.");
            }
        }

        var now = time.GetUtcNow().UtcDateTime;

        // Cancelled in its own save: the unique index allows one open invitation per address.
        var previous = await invitations.GetOpenForEmailAsync(workspaceId, email, cancellationToken);
        if (previous is not null)
        {
            previous.Cancel(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var (token, hash) = MemberInvitation.NewToken();
        var invitation = MemberInvitation.Create(workspaceId, email, hash, membership.UserId, now);
        invitations.Add(invitation);
        await notifications.MemberInvitedAsync(membership.Workspace.Name, invitation, token, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return invitation.ToResponse(now);
    }

    public async Task<IReadOnlyList<MemberInvitationResponse>> ListAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var now = time.GetUtcNow().UtcDateTime;

        return (await invitations.ListOpenAsync(workspaceId, cancellationToken)).Select(i => i.ToResponse(now)).ToList();
    }

    public async Task CancelAsync(Guid workspaceId, Guid invitationId, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var invitation = await invitations.GetByIdAsync(workspaceId, invitationId, cancellationToken)
            ?? throw new NotFoundException($"The invitation with id '{invitationId}' was not found.");

        if (invitation.AcceptedAtUtc is not null)
        {
            throw new ConflictException("This invitation was already accepted, so there is nothing to cancel.");
        }

        if (invitation.IsOpen)
        {
            invitation.Cancel(time.GetUtcNow().UtcDateTime);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
