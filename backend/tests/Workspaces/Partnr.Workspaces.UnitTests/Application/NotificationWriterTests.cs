using Partnr.Workspaces.Application.Notifications;
using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;

namespace Partnr.Workspaces.UnitTests.Application;

public class NotificationWriterTests
{
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _member = Guid.NewGuid();
    private readonly Guid _clientAccount = Guid.NewGuid();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeIdentityService _identity = new();
    private readonly Workspace _workspace;
    private readonly Client _client;
    private readonly Project _project;
    private readonly Deliverable _deliverable;
    private readonly NotificationWriter _writer;

    public NotificationWriterTests()
    {
        _workspace = Workspace.Create("Studio Pop", _owner);
        _workspace.AddMember(_member, WorkspaceRole.Member);
        _workspaces.Items.Add(_workspace);

        _client = Client.Register(_workspace.Id, "Elena", "elena@example.com");
        _client.LinkTo(_clientAccount, DateTime.UtcNow);
        _clients.Items.Add(_client);

        _project = Project.Create(_workspace.Id, _client.Id, "Website");
        _project.ChangeStatus(ProjectStatus.Active);
        _deliverable = Deliverable.Create(_workspace.Id, _project.Id, null, "Mockup", null, "https://example.com/file");

        _writer = new NotificationWriter(_notifications, _clients, _workspaces, _identity, TimeProvider.System);
    }

    [Fact]
    public async Task ApprovalRequested_NotifiesTheClientAccount()
    {
        var round = Round(requestedBy: _member);

        await _writer.ApprovalRequestedAsync(_workspace.Name, _project, _deliverable, round, CancellationToken.None);

        var notification = Assert.Single(_notifications.Items);
        Assert.Equal(NotificationType.ApprovalRequested, notification.Type);
        Assert.Equal(_clientAccount, notification.RecipientUserId);
        Assert.Equal(round.Id, notification.ApprovalRequestId);
        Assert.Equal("Studio Pop", notification.WorkspaceName);
        Assert.Equal("Website", notification.ProjectName);
        Assert.Equal("Mockup", notification.DeliverableTitle);
    }

    [Fact]
    public async Task ApprovalRequested_IsSilent_WhenTheClientCannotOpenTheProject()
    {
        var unlinked = Client.Register(_workspace.Id, "Pat", "pat@example.com");
        _clients.Items.Add(unlinked);
        var forUnlinked = Project.Create(_workspace.Id, unlinked.Id, "Unlinked");
        forUnlinked.ChangeStatus(ProjectStatus.Active);

        var archivedCard = Client.Register(_workspace.Id, "Old", "old@example.com");
        archivedCard.LinkTo(Guid.NewGuid(), DateTime.UtcNow);
        archivedCard.Archive();
        _clients.Items.Add(archivedCard);
        var forArchivedCard = Project.Create(_workspace.Id, archivedCard.Id, "Archived card");
        forArchivedCard.ChangeStatus(ProjectStatus.Active);

        var planning = Project.Create(_workspace.Id, _client.Id, "Still planning");
        var archivedProject = Project.Create(_workspace.Id, _client.Id, "Archived project");
        archivedProject.ChangeStatus(ProjectStatus.Active);
        archivedProject.Archive();

        foreach (var project in new[] { forUnlinked, forArchivedCard, planning, archivedProject })
        {
            await _writer.ApprovalRequestedAsync(_workspace.Name, project, _deliverable, Round(requestedBy: _member), CancellationToken.None);
        }

        Assert.Empty(_notifications.Items);
    }

    [Theory]
    [InlineData(ApprovalStatus.Approved, NotificationType.ApprovalApproved)]
    [InlineData(ApprovalStatus.ChangesRequested, NotificationType.ApprovalChangesRequested)]
    public async Task ApprovalDecided_NotifiesOnlyTheMemberWhoAsked_WithTheClientsName(ApprovalStatus decision, NotificationType expected)
    {
        var account = _identity.AddAccount("elena@example.com", "pw", "Elena Popescu");
        var round = Round(requestedBy: _member);
        Decide(round, decision, account.Id);

        await _writer.ApprovalDecidedAsync(_project, _deliverable, round, CancellationToken.None);

        var notification = Assert.Single(_notifications.Items);
        Assert.Equal(expected, notification.Type);
        Assert.Equal(_member, notification.RecipientUserId);
        Assert.Equal("Elena Popescu", notification.ActorName);
    }

    [Fact]
    public async Task ApprovalDecided_FallsBackToTheEmail_WhenTheAccountHasNoName()
    {
        var account = _identity.AddAccount("elena@example.com", "pw", " ");
        var round = Round(requestedBy: _member);
        Decide(round, ApprovalStatus.Approved, account.Id);

        await _writer.ApprovalDecidedAsync(_project, _deliverable, round, CancellationToken.None);

        Assert.Equal("elena@example.com", Assert.Single(_notifications.Items).ActorName);
    }

    [Fact]
    public async Task ApprovalDecided_IsSilent_WhenTheRequesterLeftTheWorkspace()
    {
        var account = _identity.AddAccount("elena@example.com", "pw", "Elena");
        var round = Round(requestedBy: Guid.NewGuid());
        Decide(round, ApprovalStatus.Approved, account.Id);

        await _writer.ApprovalDecidedAsync(_project, _deliverable, round, CancellationToken.None);

        Assert.Empty(_notifications.Items);
    }

    [Fact]
    public async Task InvitationAccepted_NotifiesTheMemberWhoInvited()
    {
        var account = _identity.AddAccount("elena@example.com", "pw", "Elena Popescu");
        var invitation = Invitation(invitedBy: _member);

        await _writer.InvitationAcceptedAsync(_workspace, _client, invitation, account.Id, CancellationToken.None);

        var notification = Assert.Single(_notifications.Items);
        Assert.Equal(NotificationType.InvitationAccepted, notification.Type);
        Assert.Equal(_member, notification.RecipientUserId);
        Assert.Equal(_client.Id, notification.ClientId);
        Assert.Equal("Elena Popescu", notification.ActorName);
    }

    [Fact]
    public async Task InvitationAccepted_IsSilent_WhenTheInviterLeftTheWorkspace()
    {
        var account = _identity.AddAccount("elena@example.com", "pw", "Elena");

        await _writer.InvitationAcceptedAsync(_workspace, _client, Invitation(invitedBy: Guid.NewGuid()), account.Id, CancellationToken.None);

        Assert.Empty(_notifications.Items);
    }

    [Fact]
    public async Task Withdrawn_LeavesTheInbox_AndStopsTheUnsentEmail_OfThatRoundOnly()
    {
        var first = Round(requestedBy: _member);
        var second = Round(requestedBy: _member);
        await _writer.ApprovalRequestedAsync(_workspace.Name, _project, _deliverable, first, CancellationToken.None);
        await _writer.ApprovalRequestedAsync(_workspace.Name, _project, _deliverable, second, CancellationToken.None);

        await _writer.ApprovalRequestWithdrawnAsync(first.Id, CancellationToken.None);

        var withdrawn = _notifications.Items.Single(n => n.ApprovalRequestId == first.Id);
        var kept = _notifications.Items.Single(n => n.ApprovalRequestId == second.Id);
        Assert.Equal(NotificationEmailStatus.Cancelled, withdrawn.EmailStatus);
        Assert.NotNull(withdrawn.ObsoleteAtUtc);
        Assert.Equal(NotificationEmailStatus.Pending, kept.EmailStatus);
        Assert.Null(kept.ObsoleteAtUtc);
    }

    [Fact]
    public async Task Withdrawn_LeavesTheInboxEvenIfTheEmailAlreadyWentOut_ButKeepsTheEmailAsSent()
    {
        var round = Round(requestedBy: _member);
        await _writer.ApprovalRequestedAsync(_workspace.Name, _project, _deliverable, round, CancellationToken.None);
        _notifications.Items.Single().MarkEmailSent(DateTime.UtcNow);

        await _writer.ApprovalRequestWithdrawnAsync(round.Id, CancellationToken.None);

        var notification = _notifications.Items.Single();
        Assert.Equal(NotificationEmailStatus.Sent, notification.EmailStatus);
        Assert.NotNull(notification.ObsoleteAtUtc);
    }

    [Fact]
    public async Task Withdrawn_OfARoundWithNoNotification_DoesNothing() =>
        await _writer.ApprovalRequestWithdrawnAsync(Guid.NewGuid(), CancellationToken.None);

    private ApprovalRequest Round(Guid requestedBy) =>
        ApprovalRequest.Request(_workspace.Id, _project.Id, _deliverable.Id, requestedBy, null);

    private static void Decide(ApprovalRequest round, ApprovalStatus decision, Guid by)
    {
        if (decision == ApprovalStatus.Approved)
        {
            round.Approve(by, null);
        }
        else
        {
            round.RequestChanges(by, "Bigger logo.");
        }
    }

    private ClientInvitation Invitation(Guid invitedBy) =>
        ClientInvitation.Create(_workspace.Id, _client.Id, _client.Email, "hash", invitedBy, DateTime.UtcNow);
}
