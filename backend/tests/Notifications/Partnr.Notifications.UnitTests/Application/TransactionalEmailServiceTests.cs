using Partnr.Contracts.Events;
using Partnr.Notifications.Application.Services;

namespace Partnr.Notifications.UnitTests.Application;

public class TransactionalEmailServiceTests
{
    private readonly FakeEmailSender _email = new();
    private readonly TransactionalEmailService _service;

    public TransactionalEmailServiceTests() => _service = new TransactionalEmailService(_email, new FakeAppLinks());

    [Fact]
    public async Task AnInvitation_GoesToTheClientAddress_WithTheLinkAndWhoInvited()
    {
        await _service.SendAsync(
            new ClientInvited("elena@acme.com", "Elena", "Studio Pop", "Ana Pop", "a-token", new DateTime(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        var message = Assert.Single(_email.Sent);
        Assert.Equal("elena@acme.com", message.To);
        Assert.Equal("Studio Pop invited you to follow your projects", message.Subject);
        Assert.StartsWith("Hi Elena,", message.TextBody);
        Assert.Contains("Ana Pop (Studio Pop) invited you to Partnr", message.TextBody);
        Assert.Contains("https://app.test/invite/a-token", message.TextBody);
        Assert.Contains("until 21 October 2026", message.TextBody);
    }

    [Fact]
    public async Task AnInvitationWithoutNames_StillReads()
    {
        await _service.SendAsync(new ClientInvited("elena@acme.com", "", "Studio Pop", "", "a-token", DateTime.UtcNow), CancellationToken.None);

        var message = Assert.Single(_email.Sent);
        Assert.StartsWith("Hi,", message.TextBody);
        Assert.Contains("Studio Pop invited you to Partnr", message.TextBody);
    }

    [Fact]
    public async Task APasswordReset_GoesToTheAccount_WithTheLink()
    {
        await _service.SendAsync(new PasswordResetRequested("ana@example.com", "Ana Pop", "r-token"), CancellationToken.None);

        var message = Assert.Single(_email.Sent);
        Assert.Equal("ana@example.com", message.To);
        Assert.Equal("Reset your Partnr password", message.Subject);
        Assert.StartsWith("Hi Ana Pop,", message.TextBody);
        Assert.Contains("https://app.test/reset-password/r-token", message.TextBody);
    }

    [Fact]
    public async Task AFailedSend_Throws_SoTheBrokerTriesAgain()
    {
        _email.Throw = new InvalidOperationException("smtp down");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SendAsync(new PasswordResetRequested("ana@example.com", "Ana", "r-token"), CancellationToken.None));
    }
}
