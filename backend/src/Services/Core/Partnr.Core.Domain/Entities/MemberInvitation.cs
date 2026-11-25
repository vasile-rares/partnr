using Partnr.Core.Domain.Common;
using Partnr.Core.Domain.Enums;
using Partnr.Core.Domain.Exceptions;

namespace Partnr.Core.Domain.Entities;

/// <summary>An invitation to join a workspace's team, always as a <see cref="WorkspaceRole.Member"/>. Bound to the address it was sent to; only the hash of the token is stored.</summary>
public sealed class MemberInvitation : Entity
{
    public static readonly TimeSpan Validity = TimeSpan.FromDays(7);

    private MemberInvitation()
    {
    }

    private MemberInvitation(Guid workspaceId, string email, string tokenHash, Guid invitedByUserId, DateTime createdAtUtc)
    {
        WorkspaceId = workspaceId;
        Email = email;
        TokenHash = tokenHash;
        InvitedByUserId = invitedByUserId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = createdAtUtc + Validity;
    }

    public Guid WorkspaceId { get; private set; }

    /// <summary>Normalized. Only an account with this email can accept it.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>SHA-256 of the token, in hex. The token itself is never stored.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public Guid InvitedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? AcceptedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public bool IsOpen => AcceptedAtUtc is null && CancelledAtUtc is null;

    public static MemberInvitation Create(Guid workspaceId, string email, string tokenHash, Guid invitedByUserId, DateTime nowUtc)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new DomainException("An invitation is for a workspace.");
        }

        if (invitedByUserId == Guid.Empty)
        {
            throw new DomainException("An invitation needs to know who sent it.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("An invitation needs an email address.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("An invitation needs a token.");
        }

        return new MemberInvitation(workspaceId, Client.NormalizeEmail(email), tokenHash, invitedByUserId, nowUtc);
    }

    public static (string Token, string Hash) NewToken() => SecureToken.New();

    public static string HashToken(string token) => SecureToken.Hash(token);

    public InvitationStatus StatusAt(DateTime nowUtc)
    {
        if (AcceptedAtUtc is not null)
        {
            return InvitationStatus.Accepted;
        }

        if (CancelledAtUtc is not null)
        {
            return InvitationStatus.Cancelled;
        }

        return nowUtc >= ExpiresAtUtc ? InvitationStatus.Expired : InvitationStatus.Pending;
    }

    public bool IsPending(DateTime nowUtc) => StatusAt(nowUtc) == InvitationStatus.Pending;

    public void Accept(DateTime nowUtc)
    {
        if (!IsPending(nowUtc))
        {
            throw new DomainException("Only a pending invitation can be accepted.");
        }

        AcceptedAtUtc = nowUtc;
    }

    /// <summary>Cancelling twice keeps the first date; an accepted one cannot be cancelled.</summary>
    public void Cancel(DateTime nowUtc)
    {
        if (AcceptedAtUtc is not null)
        {
            throw new DomainException("An accepted invitation cannot be cancelled.");
        }

        CancelledAtUtc ??= nowUtc;
    }
}
