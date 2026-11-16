using Partnr.Domain.Enums;
using Partnr.Domain.Exceptions;

namespace Partnr.Domain.Entities;

/// <summary>One account's permission to use its Google Calendar. At most one per account; the refresh token is stored only in protected form.</summary>
public sealed class GoogleConnection : Entity
{
    public const int EmailMaxLength = 320;

    private GoogleConnection()
    {
    }

    private GoogleConnection(Guid userId, string googleEmail, string protectedRefreshToken, DateTime nowUtc)
    {
        UserId = userId;
        GoogleEmail = googleEmail;
        ProtectedRefreshToken = protectedRefreshToken;
        ConnectedAtUtc = nowUtc;
        Status = GoogleConnectionStatus.Connected;
    }

    public Guid UserId { get; private set; }

    /// <summary>The Google account the permission came from, shown so the person knows which calendar it is.</summary>
    public string GoogleEmail { get; private set; } = string.Empty;

    public string ProtectedRefreshToken { get; private set; } = string.Empty;

    public GoogleConnectionStatus Status { get; private set; }

    public DateTime ConnectedAtUtc { get; private set; }

    public static GoogleConnection Connect(Guid userId, string googleEmail, string protectedRefreshToken, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A Google connection belongs to an account.");
        }

        return new GoogleConnection(userId, Clean(googleEmail), Require(protectedRefreshToken), nowUtc);
    }

    /// <summary>Connecting again replaces the permission, whichever Google account it now comes from.</summary>
    public void Reconnect(string googleEmail, string protectedRefreshToken, DateTime nowUtc)
    {
        GoogleEmail = Clean(googleEmail);
        ProtectedRefreshToken = Require(protectedRefreshToken);
        ConnectedAtUtc = nowUtc;
        Status = GoogleConnectionStatus.Connected;
    }

    public void MarkNeedsReconnect() => Status = GoogleConnectionStatus.NeedsReconnect;

    private static string Clean(string googleEmail)
    {
        var email = googleEmail?.Trim() ?? string.Empty;
        if (email.Length is 0 or > EmailMaxLength)
        {
            throw new DomainException("The Google account needs an email address.");
        }

        return email;
    }

    private static string Require(string protectedRefreshToken) =>
        string.IsNullOrWhiteSpace(protectedRefreshToken)
            ? throw new DomainException("A Google connection needs its permission token.")
            : protectedRefreshToken;
}
