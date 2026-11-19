using Partnr.Notifications.Application.Exceptions;
using Partnr.Notifications.Application.Services;
using Partnr.Notifications.Domain.Entities;

namespace Partnr.Notifications.UnitTests.Application;

public class NotificationServiceTests
{
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeInboxPublisher _inbox = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly FakeCurrentUser _currentUser;
    private readonly Guid _me = Guid.NewGuid();
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _currentUser = new FakeCurrentUser(_me);
        _service = new NotificationService(_notifications, _currentUser, _inbox, _unitOfWork, _time);
    }

    [Fact]
    public async Task List_ShowsMyNewest30_NewestFirst_WithTheCountOfEveryUnreadOne()
    {
        for (var i = 0; i < 35; i++)
        {
            Add(_me, minutesAgo: 35 - i, title: $"Deliverable {i}");
        }

        Add(Guid.NewGuid(), minutesAgo: 1, title: "Someone else's");

        var response = await _service.ListAsync(CancellationToken.None);

        Assert.Equal(35, response.Unread);
        Assert.Equal(30, response.Items.Count);
        Assert.Contains("Deliverable 34", response.Items[0].Title);
        Assert.DoesNotContain(response.Items, item => item.Title.Contains("Someone else's"));
    }

    [Fact]
    public async Task AnObsoleteNotification_IsNeitherListedNorCounted()
    {
        var gone = Add(_me, 5, "Gone");
        Add(_me, 4, "Kept");
        gone.MarkObsolete(_time.Now.UtcDateTime);

        var response = await _service.ListAsync(CancellationToken.None);

        Assert.Equal(1, response.Unread);
        Assert.Contains("Kept", Assert.Single(response.Items).Title);
        Assert.Equal(1, (await _service.UnreadCountAsync(CancellationToken.None)).Unread);
    }

    [Fact]
    public async Task MarkRead_KeepsTheFirstDate_AndTellsTheAccountsOtherTabs()
    {
        var notification = Add(_me, 5, "Mockup");

        await _service.MarkReadAsync(notification.Id, CancellationToken.None);

        Assert.Equal(_time.Now.UtcDateTime, notification.ReadAtUtc);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Equal([_me], Assert.Single(_inbox.Pushes));
    }

    [Fact]
    public async Task MarkRead_OfSomeoneElsesObsoleteOrUnknownNotification_IsNotFound()
    {
        var theirs = Add(Guid.NewGuid(), 5, "Theirs");
        var obsolete = Add(_me, 5, "Obsolete");
        obsolete.MarkObsolete(_time.Now.UtcDateTime);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkReadAsync(theirs.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkReadAsync(obsolete.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkReadAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Empty(_inbox.Pushes);
    }

    [Fact]
    public async Task MarkAllRead_MarksOnlyMine_AndSignalsOnlyWhenSomethingChanged()
    {
        Add(_me, 5, "One");
        Add(_me, 4, "Two");
        var theirs = Add(Guid.NewGuid(), 3, "Theirs");

        await _service.MarkAllReadAsync(CancellationToken.None);

        Assert.All(_notifications.Items.Where(n => n.RecipientUserId == _me), n => Assert.True(n.IsRead));
        Assert.False(theirs.IsRead);
        Assert.Single(_inbox.Pushes);

        await _service.MarkAllReadAsync(CancellationToken.None);

        Assert.Single(_inbox.Pushes);
    }

    [Fact]
    public async Task Anonymous_IsUnauthorized()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.ListAsync(CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.MarkAllReadAsync(CancellationToken.None));
    }

    private Notification Add(Guid recipient, int minutesAgo, string title)
    {
        var notification = Notification.ApprovalRequested(
            recipient, "someone@example.com", "Someone", Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", title, Guid.NewGuid(), _time.Now.UtcDateTime.AddMinutes(-minutesAgo));
        _notifications.Items.Add(notification);

        return notification;
    }
}
