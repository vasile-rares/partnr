using ClientPortal.Application.DTOs.Email;
using ClientPortal.Application.Interfaces.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ClientPortal.Infrastructure.Email;

/// <summary>Sends each message over its own SMTP connection. Simple and enough for invitations; batching can come later.</summary>
internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(settings.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();

        using var smtp = new SmtpClient();
        var security = settings.UseTls ? SecureSocketOptions.Auto : SecureSocketOptions.None;
        await smtp.ConnectAsync(settings.SmtpHost, settings.SmtpPort, security, cancellationToken);
        if (!string.IsNullOrEmpty(settings.SmtpUsername))
        {
            await smtp.AuthenticateAsync(settings.SmtpUsername, settings.SmtpPassword ?? string.Empty, cancellationToken);
        }

        await smtp.SendAsync(mime, cancellationToken);
        await smtp.DisconnectAsync(quit: true, cancellationToken);
    }
}
