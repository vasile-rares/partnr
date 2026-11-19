using Partnr.Notifications.Application.Interfaces.Services;
using Partnr.Notifications.Application.Services;
using Partnr.Notifications.Domain.Entities;
using Partnr.Notifications.Domain.Enums;

namespace Partnr.Notifications.UnitTests.Application;

public class NotificationEmailServiceTests
{
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeEmailSender _email = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly NotificationEmailService _service;

    public NotificationEmailServiceTests()
    {
        _service = new NotificationEmailService(_notifications, _email, new FakeAppLinks(), _unitOfWork, _time);
    }

    [Fact]
    public async Task ADueNotification_IsEmailedToTheAddressTheEventNamed_WithTheLinkAndNoOtherServiceAsked()
    {
        var notification = Add("elena@example.com", "Elena Client");

        var run = await _service.SendDueAsync(CancellationToken.None);

        Assert.Equal(new NotificationEmailRun(1, 0), run);
        var message = Assert.Single(_email.Sent);
        Assert.Equal("elena@example.com", message.To);
        Assert.Equal("Studio asked for your approval: Mockup", message.Subject);
        Assert.Contains("Hi Elena Client,", message.TextBody);
        Assert.Contains($"https://app.test/w/{notification.WorkspaceId}/projects/{notification.ProjectId}?tab=deliverables", message.TextBody);
        Assert.Equal(NotificationEmailStatus.Sent, notification.EmailStatus);
    }

    [Fact]
    public async Task WithoutAName_TheGreetingIsPlain()
    {
        Add("elena@example.com", null);

        await _service.SendDueAsync(CancellationToken.None);

        Assert.StartsWith("Hi,", _email.Sent[0].TextBody.TrimStart());
    }

    [Fact]
    public async Task TheMessage_IsHtmlEncoded_SoNamesCannotInjectMarkup()
    {
        Add("elena@example.com", "<b>Elena</b>");

        await _service.SendDueAsync(CancellationToken.None);

        Assert.DoesNotContain("<b>Elena</b>", _email.Sent[0].HtmlBody);
        Assert.Contains("&lt;b&gt;Elena&lt;/b&gt;", _email.Sent[0].HtmlBody);
    }

    [Fact]
    public async Task ASentNotification_ACancelledOneAndAFutureOne_AreNotSent()
    {
        Add("sent@example.com", null).MarkEmailSent(_time.Now.UtcDateTime);
        Add("cancelled@example.com", null).CancelEmail();
        Add("later@example.com", null, createdAt: _time.Now.UtcDateTime.AddMinutes(10));

        var run = await _service.SendDueAsync(CancellationToken.None);

        Assert.Equal(new NotificationEmailRun(0, 0), run);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task WhenTheSendFails_ItIsTriedAgainAfterAWhile_AndTheNextOneStillGoesOut()
    {
        var first = Add("first@example.com", null, createdAt: _time.Now.UtcDateTime.AddSeconds(-2));
        var second = Add("second@example.com", null, createdAt: _time.Now.UtcDateTime.AddSeconds(-1));
        _email.Throw = new InvalidOperationException("SMTP is down.");

        var run = await _service.SendDueAsync(CancellationToken.None);

        Assert.Equal(new NotificationEmailRun(0, 2), run);
        Assert.Equal((NotificationEmailStatus.Pending, 1, "SMTP is down."), (first.EmailStatus, first.EmailAttempts, first.EmailLastError));
        Assert.Equal(_time.Now.UtcDateTime.AddMinutes(1), first.EmailNextAttemptAtUtc);
        Assert.Equal(2, _unitOfWork.SaveCount); // one save per email: a later failure must not forget an earlier send

        _email.Throw = null;
        _time.Now = _time.Now.AddMinutes(2);
        var again = await _service.SendDueAsync(CancellationToken.None);

        Assert.Equal(new NotificationEmailRun(2, 0), again);
        Assert.Equal(NotificationEmailStatus.Sent, second.EmailStatus);
    }

    [Fact]
    public async Task AfterTheFifthFailure_ItGivesUpForGood()
    {
        var notification = Add("elena@example.com", null);
        _email.Throw = new InvalidOperationException("SMTP is down.");

        for (var attempt = 0; attempt < Notification.MaxEmailAttempts; attempt++)
        {
            await _service.SendDueAsync(CancellationToken.None);
            _time.Now = new DateTimeOffset(notification.EmailNextAttemptAtUtc, TimeSpan.Zero);
        }

        Assert.Equal(NotificationEmailStatus.Failed, notification.EmailStatus);
        Assert.Equal(new NotificationEmailRun(0, 0), await _service.SendDueAsync(CancellationToken.None));
    }

    private Notification Add(string email, string? name, DateTime? createdAt = null)
    {
        var notification = Notification.ApprovalRequested(
            Guid.NewGuid(), email, name, Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "Mockup", Guid.NewGuid(), createdAt ?? _time.Now.UtcDateTime);
        _notifications.Items.Add(notification);

        return notification;
    }
}
