using Partnr.Core.Application.DTOs.Members;

namespace Partnr.Core.Application.Interfaces.Services;

/// <summary>The owner's side of inviting people to the team. What the invited person does with the link is <see cref="IInvitationService"/>.</summary>
public interface IMemberInvitationService
{
    /// <summary>Replaces any open invitation to the same address, so only the newest link works.</summary>
    Task<MemberInvitationResponse> InviteAsync(Guid workspaceId, InviteMemberRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberInvitationResponse>> ListAsync(Guid workspaceId, CancellationToken cancellationToken);

    /// <summary>Nothing to cancel is not an error.</summary>
    Task CancelAsync(Guid workspaceId, Guid invitationId, CancellationToken cancellationToken);
}
