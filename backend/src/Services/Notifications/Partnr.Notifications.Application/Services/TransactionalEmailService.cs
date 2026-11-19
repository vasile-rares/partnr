using System.Globalization;
using Partnr.Contracts.Events;
using Partnr.Notifications.Application.DTOs.Email;
using Partnr.Notifications.Application.Interfaces.Services;

namespace Partnr.Notifications.Application.Services;

internal sealed class TransactionalEmailService(IEmailSender email, IAppLinks links) : ITransactionalEmailService
{
    private const string AppName = "Partnr";

    public Task SendAsync(ClientInvited invited, CancellationToken cancellationToken)
    {
        var from = string.IsNullOrWhiteSpace(invited.InviterName) ? invited.WorkspaceName : $"{invited.InviterName} ({invited.WorkspaceName})";
        var expires = invited.ExpiresAtUtc.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        var link = links.Url($"/invite/{Uri.EscapeDataString(invited.Token)}");

        return email.SendAsync(
            new EmailMessage(
                invited.RecipientEmail,
                $"{invited.WorkspaceName} invited you to follow your projects",
                $"""
                {Greeting(invited.RecipientName)}

                {from} invited you to {AppName}, where you can follow your projects and see what is happening.

                Open this link to create your account or sign in:
                {link}

                The link works until {expires}, and only with this email address ({invited.RecipientEmail}).
                If you were not expecting this message, you can ignore it.
                """),
            cancellationToken);
    }

    public Task SendAsync(PasswordResetRequested requested, CancellationToken cancellationToken)
    {
        var link = links.Url($"/reset-password/{Uri.EscapeDataString(requested.Token)}");

        return email.SendAsync(
            new EmailMessage(
                requested.RecipientEmail,
                $"Reset your {AppName} password",
                $"""
                {Greeting(requested.RecipientName)}

                Someone asked to reset the password of your {AppName} account ({requested.RecipientEmail}).

                Choose a new password here:
                {link}

                The link works once, for one hour. If you did not ask for this, ignore this email: your password stays as it is.
                """),
            cancellationToken);
    }

    private static string Greeting(string name) => string.IsNullOrWhiteSpace(name) ? "Hi," : $"Hi {name},";
}
