using Partnr.Core.Application.DTOs.Members;
using Partnr.Core.Domain.Entities;

namespace Partnr.Core.Application.Mappings;

public static class MemberMappings
{
    public static MemberInvitationResponse ToResponse(this MemberInvitation invitation, DateTime nowUtc) =>
        new(invitation.Id, invitation.Email, invitation.StatusAt(nowUtc).ToString(), invitation.CreatedAtUtc, invitation.ExpiresAtUtc);
}
