using Partnr.Core.Domain.Entities;
using Partnr.Core.Domain.Enums;

namespace Partnr.Core.Application.Common;

/// <summary>One of the two kinds of invitation, whichever the token of a link belongs to.</summary>
internal sealed record FoundInvitation(ClientInvitation? Client, MemberInvitation? Member)
{
    public string Kind => Client is not null ? "Client" : "Member";

    public Guid WorkspaceId => Client?.WorkspaceId ?? Member!.WorkspaceId;

    public string Email => Client?.Email ?? Member!.Email;

    public DateTime ExpiresAtUtc => Client?.ExpiresAtUtc ?? Member!.ExpiresAtUtc;

    public InvitationStatus StatusAt(DateTime nowUtc) => Client?.StatusAt(nowUtc) ?? Member!.StatusAt(nowUtc);
}
