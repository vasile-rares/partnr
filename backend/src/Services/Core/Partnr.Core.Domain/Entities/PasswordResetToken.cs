using Partnr.Core.Domain.Exceptions;

namespace Partnr.Core.Domain.Entities;

/// <summary>One "forgot password" link. Only the hash of its token is stored; it works once, for an hour, and a newer link cancels it.</summary>
public sealed class PasswordResetToken : Entity
{
    public static readonly TimeSpan Validity = TimeSpan.FromHours(1);

    /// <summary>A second link for the same account within this time is not sent, so an inbox cannot be flooded.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(1);

    private PasswordResetToken()
    {
    }

    private PasswordResetToken(Guid userId, string tokenHash, DateTime createdAtUtc)
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = createdAtUtc + Validity;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? UsedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public bool IsOpen => UsedAtUtc is null && CancelledAtUtc is null;

    public static PasswordResetToken Create(Guid userId, string tokenHash, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A password reset is for an account.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("A password reset needs a token.");
        }

        return new PasswordResetToken(userId, tokenHash, nowUtc);
    }

    public bool IsUsableAt(DateTime nowUtc) => IsOpen && nowUtc < ExpiresAtUtc;

    public bool IsCoolingDownAt(DateTime nowUtc) => IsOpen && nowUtc < CreatedAtUtc + Cooldown;

    public void Use(DateTime nowUtc)
    {
        if (!IsUsableAt(nowUtc))
        {
            throw new DomainException("This link can no longer be used.");
        }

        UsedAtUtc = nowUtc;
    }

    /// <summary>Cancelling twice keeps the first date; a used link stays used.</summary>
    public void Cancel(DateTime nowUtc)
    {
        if (UsedAtUtc is null)
        {
            CancelledAtUtc ??= nowUtc;
        }
    }
}
