using Partnr.Notifications.Domain.Entities;
using Partnr.Notifications.Domain.Enums;
using Partnr.Notifications.Domain.Exceptions;

namespace Partnr.Notifications.UnitTests.Domain;

public class NotificationTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ApprovalRequested_IsPending_DueRightAway_AndKeepsWhereTheEmailGoes()
    {
        var notification = Requested();

        Assert.Equal(NotificationType.ApprovalRequested, notification.Type);
        Assert.Equal(NotificationEmailStatus.Pending, notification.EmailStatus);
        Assert.Equal(0, notification.EmailAttempts);
        Assert.Equal(Now, notification.EmailNextAttemptAtUtc);
        Assert.Null(notification.ActorName);
        Assert.Equal(("elena@example.com", "Elena"), (notification.RecipientEmail, notification.RecipientName));
    }

    [Theory]
    [InlineData(true, NotificationType.ApprovalApproved)]
    [InlineData(false, NotificationType.ApprovalChangesRequested)]
    public void ApprovalDecided_TakesItsTypeFromTheDecision(bool approved, NotificationType expected)
    {
        var notification = Notification.ApprovalDecided(
            approved, Guid.NewGuid(), "ana@example.com", "Ana", Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "Mockup", Guid.NewGuid(), "Elena", Now);

        Assert.Equal(expected, notification.Type);
        Assert.Equal("Elena", notification.ActorName);
    }

    [Fact]
    public void InvitationAccepted_KeepsTheClient_NotAProject()
    {
        var clientId = Guid.NewGuid();

        var notification = Notification.InvitationAccepted(Guid.NewGuid(), "ana@example.com", "Ana", Guid.NewGuid(), "Studio", clientId, "Elena", Now);

        Assert.Equal(clientId, notification.ClientId);
        Assert.Null(notification.ProjectId);
        Assert.Null(notification.ApprovalRequestId);
    }

    [Fact]
    public void TheSameEventForTheSameAccount_HasTheSameKey_AndAnotherOneDoesNot()
    {
        var round = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        var workspace = Guid.NewGuid();
        var project = Guid.NewGuid();

        string KeyOf(bool approved, Guid who, Guid roundId) => Notification.ApprovalDecided(
            approved, who, "a@example.com", null, workspace, "Studio", project, "Website", "Mockup", roundId, "Elena", Now).DedupeKey;

        Assert.Equal(KeyOf(true, recipient, round), KeyOf(true, recipient, round));
        Assert.NotEqual(KeyOf(true, recipient, round), KeyOf(false, recipient, round));
        Assert.NotEqual(KeyOf(true, recipient, round), KeyOf(true, Guid.NewGuid(), round));
        Assert.NotEqual(KeyOf(true, recipient, round), KeyOf(true, recipient, Guid.NewGuid()));
    }

    [Fact]
    public void Names_AreOneLine_Trimmed_AndCut_BecauseTheyEndUpInASubject()
    {
        var notification = Notification.ApprovalRequested(
            Guid.NewGuid(), "elena@example.com", "  Elena\r\nC  ", Guid.NewGuid(), "  Studio\r\nPop  ", Guid.NewGuid(), "Web\tsite", new string('x', 500), Guid.NewGuid(), Now);

        Assert.Equal("Elena C", notification.RecipientName);
        Assert.Equal("Studio Pop", notification.WorkspaceName);
        Assert.Equal("Web site", notification.ProjectName);
        Assert.Equal(Notification.NameMaxLength, notification.DeliverableTitle!.Length);
    }

    [Fact]
    public void ANotificationNeedsARecipient_TheirEmail_AWorkspace_AndNames()
    {
        var round = Guid.NewGuid();

        Notification Make(Guid? user = null, string email = "elena@example.com", Guid? workspace = null, string workspaceName = "Studio", Guid? project = null, string title = "Mockup") =>
            Notification.ApprovalRequested(user ?? Guid.NewGuid(), email, "Elena", workspace ?? Guid.NewGuid(), workspaceName, project ?? Guid.NewGuid(), "Website", title, round, Now);

        Assert.Throws<DomainException>(() => Make(user: Guid.Empty));
        Assert.Throws<DomainException>(() => Make(email: " "));
        Assert.Throws<DomainException>(() => Make(email: new string('a', Notification.EmailMaxLength + 1)));
        Assert.Throws<DomainException>(() => Make(workspace: Guid.Empty));
        Assert.Throws<DomainException>(() => Make(workspaceName: " "));
        Assert.Throws<DomainException>(() => Make(title: " "));
        Assert.Throws<DomainException>(() => Make(project: Guid.Empty));
        Assert.Throws<DomainException>(() => Notification.InvitationAccepted(Guid.NewGuid(), "a@example.com", null, Guid.NewGuid(), "Studio", Guid.Empty, "Elena", Now));
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
        Notification.ApprovalRequested(Guid.NewGuid(), "elena@example.com", "Elena", Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "Mockup", Guid.NewGuid(), Now);
}
