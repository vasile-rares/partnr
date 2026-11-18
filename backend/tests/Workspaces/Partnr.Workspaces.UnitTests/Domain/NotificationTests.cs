using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;
using Partnr.Workspaces.Domain.Exceptions;

namespace Partnr.Workspaces.UnitTests.Domain;

public class NotificationTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ApprovalRequested_IsPending_AndDueRightAway()
    {
        var notification = Requested();

        Assert.Equal(NotificationType.ApprovalRequested, notification.Type);
        Assert.Equal(NotificationEmailStatus.Pending, notification.EmailStatus);
        Assert.Equal(0, notification.EmailAttempts);
        Assert.Equal(notification.CreatedAtUtc, notification.EmailNextAttemptAtUtc);
        Assert.Null(notification.ActorName);
    }

    [Theory]
    [InlineData(ApprovalStatus.Approved, NotificationType.ApprovalApproved)]
    [InlineData(ApprovalStatus.ChangesRequested, NotificationType.ApprovalChangesRequested)]
    public void ApprovalDecided_TakesItsTypeFromTheDecision(ApprovalStatus decision, NotificationType expected)
    {
        var notification = Notification.ApprovalDecided(
            decision, Guid.NewGuid(), Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "Mockup", Guid.NewGuid(), "Elena");

        Assert.Equal(expected, notification.Type);
        Assert.Equal("Elena", notification.ActorName);
    }

    [Theory]
    [InlineData(ApprovalStatus.Pending)]
    [InlineData(ApprovalStatus.Withdrawn)]
    public void ApprovalDecided_RefusesAnythingButADecision(ApprovalStatus status) =>
        Assert.Throws<DomainException>(() => Notification.ApprovalDecided(
            status, Guid.NewGuid(), Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "Mockup", Guid.NewGuid(), "Elena"));

    [Fact]
    public void InvitationAccepted_KeepsTheClient_NotAProject()
    {
        var clientId = Guid.NewGuid();

        var notification = Notification.InvitationAccepted(Guid.NewGuid(), Guid.NewGuid(), "Studio", clientId, "Elena");

        Assert.Equal(clientId, notification.ClientId);
        Assert.Null(notification.ProjectId);
        Assert.Null(notification.ApprovalRequestId);
    }

    [Fact]
    public void Names_AreOneLine_Trimmed_AndCut_BecauseTheyEndUpInASubject()
    {
        var notification = Notification.ApprovalRequested(
            Guid.NewGuid(), Guid.NewGuid(), "  Studio\r\nPop  ", Guid.NewGuid(), "Web\tsite", new string('x', 500), Guid.NewGuid());

        Assert.Equal("Studio Pop", notification.WorkspaceName);
        Assert.Equal("Web site", notification.ProjectName);
        Assert.Equal(Notification.NameMaxLength, notification.DeliverableTitle!.Length);
    }

    [Fact]
    public void ANotificationNeedsARecipient_AWorkspace_AndNames()
    {
        Assert.Throws<DomainException>(() => Notification.ApprovalRequested(Guid.Empty, Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "Mockup", Guid.NewGuid()));
        Assert.Throws<DomainException>(() => Notification.ApprovalRequested(Guid.NewGuid(), Guid.Empty, "Studio", Guid.NewGuid(), "Website", "Mockup", Guid.NewGuid()));
        Assert.Throws<DomainException>(() => Notification.ApprovalRequested(Guid.NewGuid(), Guid.NewGuid(), " ", Guid.NewGuid(), "Website", "Mockup", Guid.NewGuid()));
        Assert.Throws<DomainException>(() => Notification.ApprovalRequested(Guid.NewGuid(), Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", " ", Guid.NewGuid()));
        Assert.Throws<DomainException>(() => Notification.ApprovalRequested(Guid.NewGuid(), Guid.NewGuid(), "Studio", Guid.Empty, "Website", "Mockup", Guid.NewGuid()));
        Assert.Throws<DomainException>(() => Notification.InvitationAccepted(Guid.NewGuid(), Guid.NewGuid(), "Studio", Guid.Empty, "Elena"));
    }

    [Fact]
    public void MarkEmailSent_KeepsTheDate_AndClearsTheLastError()
    {
        var notification = Requested();
        notification.RecordEmailFailure(Now, "SMTP is down.");

        notification.MarkEmailSent(Now.AddMinutes(1));

        Assert.Equal(NotificationEmailStatus.Sent, notification.EmailStatus);
        Assert.Equal(2, notification.EmailAttempts);
        Assert.Equal(Now.AddMinutes(1), notification.EmailSentAtUtc);
        Assert.Null(notification.EmailLastError);
    }

    [Fact]
    public void EachFailure_WaitsLongerBeforeTheNextAttempt_AndTheFifthIsFinal()
    {
        var notification = Requested();
        var waits = new List<TimeSpan>();

        for (var i = 1; i < Notification.MaxEmailAttempts; i++)
        {
            notification.RecordEmailFailure(Now, "SMTP is down.");
            Assert.Equal(NotificationEmailStatus.Pending, notification.EmailStatus);
            waits.Add(notification.EmailNextAttemptAtUtc - Now);
        }

        notification.RecordEmailFailure(Now, "SMTP is down.");

        Assert.Equal(new[] { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60) }, waits);
        Assert.Equal(NotificationEmailStatus.Failed, notification.EmailStatus);
        Assert.Equal(Notification.MaxEmailAttempts, notification.EmailAttempts);
    }

    [Fact]
    public void ALongError_IsCutToWhatTheColumnHolds()
    {
        var notification = Requested();

        notification.RecordEmailFailure(Now, new string('e', 2000));

        Assert.Equal(Notification.ErrorMaxLength, notification.EmailLastError!.Length);
    }

    [Fact]
    public void CancelEmail_StopsAPendingOne_ButLeavesASentOneAlone()
    {
        var pending = Requested();
        var sent = Requested();
        sent.MarkEmailSent(Now);

        pending.CancelEmail();
        sent.CancelEmail();

        Assert.Equal(NotificationEmailStatus.Cancelled, pending.EmailStatus);
        Assert.Equal(NotificationEmailStatus.Sent, sent.EmailStatus);
    }

    [Fact]
    public void MarkRead_KeepsTheFirstDate()
    {
        var notification = Requested();
        Assert.False(notification.IsRead);

        notification.MarkRead(Now);
        notification.MarkRead(Now.AddHours(1));

        Assert.True(notification.IsRead);
        Assert.Equal(Now, notification.ReadAtUtc);
    }

    [Fact]
    public void MarkObsolete_CancelsAPendingEmail_ButNotOneThatWentOut()
    {
        var pending = Requested();
        var sent = Requested();
        sent.MarkEmailSent(Now);

        pending.MarkObsolete(Now);
        sent.MarkObsolete(Now);
        sent.MarkObsolete(Now.AddHours(1));

        Assert.Equal(NotificationEmailStatus.Cancelled, pending.EmailStatus);
        Assert.Equal(NotificationEmailStatus.Sent, sent.EmailStatus);
        Assert.Equal(Now, sent.ObsoleteAtUtc); // the first date stays
    }

    private static Notification Requested() =>
        Notification.ApprovalRequested(Guid.NewGuid(), Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "Mockup", Guid.NewGuid());
}
