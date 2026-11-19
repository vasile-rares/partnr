using Partnr.Contracts.Events;
using Partnr.Notifications.Application.Services;
using Partnr.Notifications.Domain.Enums;

namespace Partnr.Notifications.UnitTests.Application;

public class NotificationEventServiceTests
{
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeInboxPublisher _inbox = new();
    private readonly FakeConversationPublisher _conversations = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly NotificationEventService _service;

    private readonly Guid _client = Guid.NewGuid();
    private readonly Guid _member = Guid.NewGuid();
    private readonly Guid _round = Guid.NewGuid();

    public NotificationEventServiceTests()
    {
        _service = new NotificationEventService(_notifications, _inbox, _conversations, _unitOfWork, _time);
    }

    [Fact]
    public async Task ARequestForApproval_IsStored_ForTheClient_AndTheirInboxIsToldAfterTheSave()
    {
        await _service.ApplyAsync(Requested(), CancellationToken.None);

        var notification = Assert.Single(_notifications.Items);
        Assert.Equal((NotificationType.ApprovalRequested, _client, "elena@example.com", "Website"), (notification.Type, notification.RecipientUserId, notification.RecipientEmail, notification.ProjectName));
        Assert.Equal(_time.Now.UtcDateTime, notification.CreatedAtUtc);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Equal([_client], Assert.Single(_inbox.Pushes));
    }

    [Fact]
    public async Task TheSameEventDeliveredTwice_IsOneNotification_AndOneSignal()
    {
        await _service.ApplyAsync(Requested(), CancellationToken.None);
        await _service.ApplyAsync(Requested(), CancellationToken.None);

        Assert.Single(_notifications.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Single(_inbox.Pushes);
    }

    [Theory]
    [InlineData(ApprovalDecision.Approved, NotificationType.ApprovalApproved)]
    [InlineData(ApprovalDecision.ChangesRequested, NotificationType.ApprovalChangesRequested)]
    public async Task TheClientsAnswer_IsStored_ForTheMemberWhoAsked(ApprovalDecision decision, NotificationType expected)
    {
        await _service.ApplyAsync(Decided(decision), CancellationToken.None);

        var notification = Assert.Single(_notifications.Items);
        Assert.Equal((expected, _member, "Elena"), (notification.Type, notification.RecipientUserId, notification.ActorName));
        Assert.Equal([_member], Assert.Single(_inbox.Pushes));
    }

    [Fact]
    public async Task AnAcceptedInvitation_IsStored_ForTheMemberWhoInvited()
    {
        var clientCard = Guid.NewGuid();

        await _service.ApplyAsync(new InvitationAccepted(Guid.NewGuid(), "Studio", clientCard, "Elena", _member, "ana@example.com", "Ana"), CancellationToken.None);

        var notification = Assert.Single(_notifications.Items);
        Assert.Equal((NotificationType.InvitationAccepted, clientCard, _member), (notification.Type, notification.ClientId, notification.RecipientUserId));
    }

    [Fact]
    public async Task ARequestTakenBack_LeavesTheInbox_CancelsTheEmail_AndTheInboxIsTold()
    {
        await _service.ApplyAsync(Requested(), CancellationToken.None);
        _inbox.Pushes.Clear();

        await _service.WithdrawAsync(_round, CancellationToken.None);

        var notification = Assert.Single(_notifications.Items);
        Assert.NotNull(notification.ObsoleteAtUtc);
        Assert.Equal(NotificationEmailStatus.Cancelled, notification.EmailStatus);
        Assert.Equal([_client], Assert.Single(_inbox.Pushes));
    }

    [Fact]
    public async Task ARequestTakenBackBeforeTheEventThatAskedArrived_LeavesNothingToTell()
    {
        await _service.WithdrawAsync(_round, CancellationToken.None);

        await _service.ApplyAsync(Requested(), CancellationToken.None);

        Assert.Empty(_notifications.Items);
        Assert.Empty(_inbox.Pushes);
    }

    [Fact]
    public async Task TakingBackTwice_IsRememberedOnce_AndDeliveringItAgainChangesNothing()
    {
        await _service.WithdrawAsync(_round, CancellationToken.None);
        var saved = _unitOfWork.SaveCount;

        await _service.WithdrawAsync(_round, CancellationToken.None);

        Assert.Single(_notifications.Withdrawn);
        Assert.Equal(saved, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnEmailThatAlreadyWentOut_StaysSent_WhenTheRequestIsTakenBack()
    {
        await _service.ApplyAsync(Requested(), CancellationToken.None);
        _notifications.Items[0].MarkEmailSent(_time.Now.UtcDateTime);

        await _service.WithdrawAsync(_round, CancellationToken.None);

        Assert.Equal(NotificationEmailStatus.Sent, _notifications.Items[0].EmailStatus);
        Assert.NotNull(_notifications.Items[0].ObsoleteAtUtc);
    }

    [Fact]
    public async Task AChangedConversation_OnlyPushes_ToTheseAccounts_AndStoresNothing()
    {
        var project = Guid.NewGuid();

        await _service.ApplyAsync(new ConversationChanged(project, [_client, _member]), CancellationToken.None);

        Assert.Empty(_notifications.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
        var push = Assert.Single(_conversations.Pushes);
        Assert.Equal([_client, _member], push.UserIds);
        Assert.Equal(project, push.ProjectId);
    }

    private ApprovalRequested Requested() =>
        new(_round, Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "Mockup", _client, "elena@example.com", "Elena");

    private ApprovalDecided Decided(ApprovalDecision decision) =>
        new(_round, decision, Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "Mockup", "Elena", _member, "ana@example.com", "Ana");
}
