namespace Partnr.Contracts.Events;

// Emails that carry a secret link. The service that issued the link says so with everything the email needs (the address and name as they
// were at that moment, the token), and the Notifications service writes and sends it. Delivery is at least once, so a repeated event may
// send the same email twice, which does no harm: the link is the same.

/// <summary>A member invited a client; <paramref name="Token"/> is the one that goes into the link (the invitation keeps only its hash).</summary>
public sealed record ClientInvited(
    string RecipientEmail,
    string RecipientName,
    string WorkspaceName,
    string InviterName,
    string Token,
    DateTime ExpiresAtUtc);

/// <summary>The owner invited someone to join the team; <paramref name="Token"/> goes into the link (the invitation keeps only its hash).</summary>
public sealed record MemberInvited(
    string RecipientEmail,
    string WorkspaceName,
    string InviterName,
    string Token,
    DateTime ExpiresAtUtc);

/// <summary>An account asked to reset its password; <paramref name="Token"/> goes into the link and works once, for one hour.</summary>
public sealed record PasswordResetRequested(string RecipientEmail, string RecipientName, string Token);
