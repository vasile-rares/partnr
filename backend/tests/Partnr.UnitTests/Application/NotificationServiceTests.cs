using Partnr.Application.Exceptions;
using Partnr.Application.Services;
using Partnr.Domain.Entities;

namespace Partnr.UnitTests.Application;

public class NotificationServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _currentUser = new FakeCurrentUser(_me);
        _service = new NotificationService(_notifications, _currentUser, _unitOfWork, _time);
    }

    [Fact]
    public async Task List_ShowsOnlyMyOwn_NewestFirst_WithTheTextAndWhereItLeads()
    {
        var older = Add(_me, "Mockup");
        await Task.Delay(5); // created with the real clock: make the order unambiguous
        var newer = Add(_me, "Logo");
        Add(Guid.NewGuid(), "Someone else's");

        var response = await _service.ListAsync(CancellationToken.None);

        Assert.Equal(2, response.Unread);
        Assert.Equal(new[] { newer.Id, older.Id }, response.Items.Select(i => i.Id));
        var first = response.Items[0];
        Assert.Equal("Studio Pop asked for your approval: Logo", first.Title);
        Assert.Equal("Studio Pop is waiting for your approval of \"Logo\" in Website.", first.Text);
        Assert.Equal($"/portal/{newer.WorkspaceId}/projects/{newer.ProjectId}?tab=deliverables", first.Path);
        Assert.Equal("Studio Pop", first.WorkspaceName);
        Assert.False(first.IsRead);
    }

    [Fact]
    public async Task List_NeverShowsAnObsoleteOne_AndTheUnreadCountSkipsItToo()
    {
        Add(_me, "Kept");
        Add(_me, "Taken back").MarkObsolete(DateTime.UtcNow);

        var response = await _service.ListAsync(CancellationToken.None);

        Assert.Equal(1, response.Unread);
        Assert.Equal("Studio Pop asked for your approval: Kept", Assert.Single(response.Items).Title);
    }

    [Fact]
    public async Task List_ShowsThe30Newest_ButCountsEveryUnreadOne()
    {
        for (var i = 0; i < 35; i++)
        {
            Add(_me, $"Item {i}");
        }

        var response = await _service.ListAsync(CancellationToken.None);

        Assert.Equal(30, response.Items.Count);
        Assert.Equal(35, response.Unread);
    }

    [Fact]
    public async Task UnreadCount_CountsOnlyMyUnreadOnes()
    {
        Add(_me, "Unread");
        Add(_me, "Read").MarkRead(DateTime.UtcNow);
        Add(Guid.NewGuid(), "Someone else's");

        Assert.Equal(1, (await _service.UnreadCountAsync(CancellationToken.None)).Unread);
    }

    [Fact]
    public async Task MarkRead_SetsTheTime_AndSaves()
    {
        var notification = Add(_me, "Mockup");

        await _service.MarkReadAsync(notification.Id, CancellationToken.None);

        Assert.Equal(_time.Now.UtcDateTime, notification.ReadAtUtc);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task MarkRead_OfSomeoneElsesAnObsoleteOrAnUnknownOne_IsNotFound()
    {
        var theirs = Add(Guid.NewGuid(), "Theirs");
        var obsolete = Add(_me, "Taken back");
        obsolete.MarkObsolete(DateTime.UtcNow);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkReadAsync(theirs.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkReadAsync(obsolete.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkReadAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Null(theirs.ReadAtUtc);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task MarkAllRead_MarksOnlyMyUnreadOnes_WithOneSave()
    {
        var mine = Add(_me, "Mine");
        var alreadyRead = Add(_me, "Already read");
        var firstReadAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        alreadyRead.MarkRead(firstReadAt);
        var theirs = Add(Guid.NewGuid(), "Theirs");

        await _service.MarkAllReadAsync(CancellationToken.None);

        Assert.Equal(_time.Now.UtcDateTime, mine.ReadAtUtc);
        Assert.Equal(firstReadAt, alreadyRead.ReadAtUtc);
        Assert.Null(theirs.ReadAtUtc);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Anonymous_IsUnauthorized()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.ListAsync(CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.MarkAllReadAsync(CancellationToken.None));
    }

    private Notification Add(Guid recipient, string deliverable)
    {
        var notification = Notification.ApprovalRequested(
            recipient, Guid.NewGuid(), "Studio Pop", Guid.NewGuid(), "Website", deliverable, Guid.NewGuid());
        _notifications.Add(notification);

        return notification;
    }
}
