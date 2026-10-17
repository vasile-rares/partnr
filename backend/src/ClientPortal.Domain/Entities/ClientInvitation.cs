using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.Domain.Entities;

/// <summary>Bound to the address it was sent to. Only the hash of the token is stored, so a leaked database leaks no working links.</summary>
public sealed class ClientInvitation : Entity
{
    public static readonly TimeSpan Validity = TimeSpan.FromDays(7);

    private ClientInvitation()
    {
    }

    private ClientInvitation(
        Guid workspaceId,
        Guid clientId,
        string email,
        string tokenHash,
        Guid invitedByUserId,
        DateTime createdAtUtc)
    {
        WorkspaceId = workspaceId;
        ClientId = clientId;
        Email = email;
        TokenHash = tokenHash;
        InvitedByUserId = invitedByUserId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = createdAtUtc + Validity;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid ClientId { get; private set; }

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

    public static ClientInvitation Create(
        Guid workspaceId,
        Guid clientId,
        string email,
        string tokenHash,
        Guid invitedByUserId,
        DateTime nowUtc)
    {
        if (workspaceId == Guid.Empty || clientId == Guid.Empty)
        {
            throw new DomainException("An invitation is for a client of a workspace.");
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

        return new ClientInvitation(workspaceId, clientId, Client.NormalizeEmail(email), tokenHash, invitedByUserId, nowUtc);
    }

    /// <summary>A longer token cannot be one of ours: reject it early.</summary>
    public const int TokenMaxLength = 100;

    /// <summary>256 random bits, URL-safe. The token goes in the email, only its hash is stored.</summary>
    public static (string Token, string Hash) NewToken()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        return (token, HashToken(token));
    }

    /// <summary>SHA-256 in hex: the one definition of what a link has to match.</summary>
    public static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

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
