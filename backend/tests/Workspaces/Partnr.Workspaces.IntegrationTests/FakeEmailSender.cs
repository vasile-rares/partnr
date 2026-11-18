using System.Collections.Concurrent;
using Partnr.Workspaces.Application.DTOs.Email;
using Partnr.Workspaces.Application.Interfaces.Services;

namespace Partnr.Workspaces.IntegrationTests;

/// <summary>Keeps the emails in memory. Shared by all tests (they run in parallel), so tests look up messages by recipient.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> SentTo(string email) =>
        _sent.Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase)).ToList();
}
