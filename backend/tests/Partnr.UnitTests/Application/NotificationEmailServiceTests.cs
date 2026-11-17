using Partnr.Application.Interfaces.Services;
using Partnr.Application.Services;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;

namespace Partnr.UnitTests.Application;

public class NotificationEmailServiceTests
{
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeIdentityService _identity = new();
    private readonly FakeEmailSender _email = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly NotificationEmailService _service;

    public NotificationEmailServiceTests()
    {
        // Notifications are stamped with the real clock, so the fake one has to be past it for them to be due.
        _time.Now = DateTimeOffset.UtcNow.AddMinutes(1);
        _service = new NotificationEmailService(_notifications, _identity, _email, new FakeAppLinks(), _unitOfWork, _time);
    }

    [Fact]
    public async Task ADueNotification_IsEmailedToTheRecipient_WithTheSubjectAndALinkToThePage()
    {
        var elena = _identity.AddAccount("elena@example.com", "pw", "Elena Popescu");
        var notification = Requested(elena.Id);

        var run = await _service.SendDueAsync(CancellationToken.None);

        var message = Assert.Single(_email.Sent);
        Assert.Equal("elena@example.com", message.To);
        Assert.Equal("Studio Pop asked for your approval: Homepage mockups", message.Subject);
        Assert.StartsWith("Hi Elena Popescu,", message.TextBody);
        Assert.Contains("Studio Pop is waiting for your approval of \"Homepage mockups\" in Website.", message.TextBody);
        Assert.Contains($"https://app.test/w/{notification.WorkspaceId}/projects/{notification.ProjectId}?tab=deliverables", message.TextBody);
        Assert.Contains("<a href=", message.HtmlBody);
        Assert.Equal(NotificationEmailStatus.Sent, notification.EmailStatus);
        Assert.Equal(_time.Now.UtcDateTime, notification.EmailSentAtUtc);
        Assert.Equal(new NotificationEmailRun(1, 0), run);
    }

    [Fact]
    public async Task TheFreelancersEmails_LinkIntoTheirOwnApp_NotThePortal()
    {
        var pat = _identity.AddAccount("pat@example.com", "pw", "Pat");
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        _notifications.Add(Notification.ApprovalDecided(
            ApprovalStatus.ChangesRequested, pat.Id, workspaceId, "Studio Pop", projectId, "Website", "Mockup", Guid.NewGuid(), "Elena"));
        _notifications.Add(Notification.InvitationAccepted(pat.Id, workspaceId, "Studio Pop", clientId, "Elena"));

        await _service.SendDueAsync(CancellationToken.None);

        Assert.Equal(2, _email.Sent.Count);
        Assert.Equal("Elena requested changes to Mockup", _email.Sent[0].Subject);
        Assert.Contains($"https://app.test/w/{workspaceId}/projects/{projectId}?tab=deliverables", _email.Sent[0].TextBody);
        Assert.Equal("Elena joined your client portal", _email.Sent[1].Subject);
        Assert.Contains($"https://app.test/w/{workspaceId}/clients/{clientId}", _email.Sent[1].TextBody);
    }

    [Fact]
    public async Task TheHtmlVersion_EscapesWhatUsersTyped()
    {
        var elena = _identity.AddAccount("elena@example.com", "pw", "Elena");
        _notifications.Add(Notification.ApprovalRequested(
            elena.Id, Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "<script>alert(1)</script>", Guid.NewGuid()));

        await _service.SendDueAsync(CancellationToken.None);

        var message = Assert.Single(_email.Sent);
        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.Contains("&lt;script&gt;", message.HtmlBody);
    }

    [Fact]
    public async Task AFailedSend_IsKeptForARetry_WithTheError_AndTheNextOneStillGoesOut()
    {
        var broken = _identity.AddAccount("broken@example.com", "pw", "Broken");
        var fine = _identity.AddAccount("fine@example.com", "pw", "Fine");
        _email.FailFor.Add("broken@example.com");
        var first = Requested(broken.Id);
        var second = Requested(fine.Id);

        var run = await _service.SendDueAsync(CancellationToken.None);

        Assert.Equal(new NotificationEmailRun(1, 1), run);
        Assert.Equal(NotificationEmailStatus.Pending, first.EmailStatus);
        Assert.Equal(1, first.EmailAttempts);
        Assert.Equal("SMTP is down.", first.EmailLastError);
        Assert.Equal(NotificationEmailStatus.Sent, second.EmailStatus);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ARetry_WaitsForItsTime_ThenGoesOutOnceTheProblemIsGone()
    {
        var elena = _identity.AddAccount("elena@example.com", "pw", "Elena");
        _email.FailFor.Add("elena@example.com");
        var notification = Requested(elena.Id);
        await _service.SendDueAsync(CancellationToken.None);

        await _service.SendDueAsync(CancellationToken.None);
        Assert.Equal(1, notification.EmailAttempts); // not due yet

        _email.FailFor.Clear();
        _time.Now = _time.Now.AddMinutes(2);
        await _service.SendDueAsync(CancellationToken.None);

        Assert.Equal(NotificationEmailStatus.Sent, notification.EmailStatus);
        Assert.Equal(2, notification.EmailAttempts);
        Assert.Single(_email.Sent);
    }

    [Fact]
    public async Task AfterTheLastAttempt_TheNotificationIsFailed_AndNeverPickedUpAgain()
    {
        var elena = _identity.AddAccount("elena@example.com", "pw", "Elena");
        _email.FailFor.Add("elena@example.com");
        var notification = Requested(elena.Id);

        for (var i = 0; i < Notification.MaxEmailAttempts; i++)
        {
            await _service.SendDueAsync(CancellationToken.None);
            _time.Now = _time.Now.AddHours(2);
        }

        _email.FailFor.Clear();
        await _service.SendDueAsync(CancellationToken.None);

        Assert.Equal(NotificationEmailStatus.Failed, notification.EmailStatus);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task ARecipientWhoNoLongerExists_IsCancelled_NotRetried()
    {
        var notification = Requested(Guid.NewGuid());

        var run = await _service.SendDueAsync(CancellationToken.None);

        Assert.Equal(NotificationEmailStatus.Cancelled, notification.EmailStatus);
        Assert.Empty(_email.Sent);
        Assert.Equal(new NotificationEmailRun(0, 0), run);
    }

    [Fact]
    public async Task ACancelledOrSentNotification_IsLeftAlone()
    {
        var elena = _identity.AddAccount("elena@example.com", "pw", "Elena");
        Requested(elena.Id).CancelEmail();
        var sent = Requested(elena.Id);
        sent.MarkEmailSent(_time.Now.UtcDateTime);

        await _service.SendDueAsync(CancellationToken.None);

        Assert.Empty(_email.Sent);
    }

    private Notification Requested(Guid recipient)
    {
        var notification = Notification.ApprovalRequested(
            recipient, Guid.NewGuid(), "Studio Pop", Guid.NewGuid(), "Website", "Homepage mockups", Guid.NewGuid());
        _notifications.Add(notification);

        return notification;
    }
}
