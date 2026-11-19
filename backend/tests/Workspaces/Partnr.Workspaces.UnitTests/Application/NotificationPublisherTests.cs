using Partnr.Contracts.Events;
using Partnr.Workspaces.Application.Notifications;
using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;

namespace Partnr.Workspaces.UnitTests.Application;

// Who is told what: the rules only this service knows. The Notifications service stores, emails and pushes what these events say.
public class NotificationPublisherTests
{
    private readonly Guid _owner = Guid.NewGuid();
    private readonly FakeEventPublisher _events = new();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeIdentityService _identity = new();
    private readonly Guid _member;
    private readonly Guid _clientAccount;
    private readonly Workspace _workspace;
    private readonly Client _client;
    private readonly Project _project;
    private readonly Deliverable _deliverable;
    private readonly NotificationPublisher _publisher;

    public NotificationPublisherTests()
    {
        _member = _identity.AddAccount("mihai@example.com", "pw", "Mihai Member").Id;
        _clientAccount = _identity.AddAccount("elena@example.com", "pw", "Elena Client").Id;

        _workspace = Workspace.Create("Studio Pop", _owner);
        _workspace.AddMember(_member, WorkspaceRole.Member);
        _workspaces.Items.Add(_workspace);

        _client = Client.Register(_workspace.Id, "Elena", "elena@example.com");
        _client.LinkTo(_clientAccount, DateTime.UtcNow);
        _clients.Items.Add(_client);

        _project = Project.Create(_workspace.Id, _client.Id, "Website");
        _project.ChangeStatus(ProjectStatus.Active);
        _deliverable = Deliverable.Create(_workspace.Id, _project.Id, null, "Mockup", null, "https://example.com/file");

        _publisher = new NotificationPublisher(_events, _clients, _workspaces, _identity);
    }

    [Fact]
    public async Task ApprovalRequested_TellsTheClientAccount_WithTheEmailAndNameToWriteTo()
    {
        var round = Round(requestedBy: _member);

        await _publisher.ApprovalRequestedAsync(_workspace.Name, _project, _deliverable, round, CancellationToken.None);

        var sent = Assert.IsType<ApprovalRequested>(Assert.Single(_events.Published));
        Assert.Equal((round.Id, _workspace.Id, "Studio Pop", _project.Id, "Website", "Mockup"), (sent.ApprovalRequestId, sent.WorkspaceId, sent.WorkspaceName, sent.ProjectId, sent.ProjectName, sent.DeliverableTitle));
        Assert.Equal((_clientAccount, "elena@example.com", "Elena Client"), (sent.RecipientUserId, sent.RecipientEmail, sent.RecipientName));
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
            await _publisher.ApprovalRequestedAsync(_workspace.Name, project, _deliverable, Round(requestedBy: _member), CancellationToken.None);
        }

        Assert.Empty(_events.Published);
    }

    [Theory]
    [InlineData(ApprovalStatus.Approved, ApprovalDecision.Approved)]
    [InlineData(ApprovalStatus.ChangesRequested, ApprovalDecision.ChangesRequested)]
    public async Task ApprovalDecided_TellsOnlyTheMemberWhoAsked_WithTheClientsName(ApprovalStatus decision, ApprovalDecision expected)
    {
        var account = _identity.AddAccount("elena2@example.com", "pw", "Elena Popescu");
        var round = Round(requestedBy: _member);
        Decide(round, decision, account.Id);

        await _publisher.ApprovalDecidedAsync(_project, _deliverable, round, CancellationToken.None);

        var sent = Assert.IsType<ApprovalDecided>(Assert.Single(_events.Published));
        Assert.Equal((round.Id, expected, "Elena Popescu"), (sent.ApprovalRequestId, sent.Decision, sent.ActorName));
        Assert.Equal((_member, "mihai@example.com", "Mihai Member"), (sent.RecipientUserId, sent.RecipientEmail, sent.RecipientName));
    }

    [Fact]
    public async Task ApprovalDecided_FallsBackToTheEmail_WhenTheAccountHasNoName()
    {
        var account = _identity.AddAccount("elena2@example.com", "pw", " ");
        var round = Round(requestedBy: _member);
        Decide(round, ApprovalStatus.Approved, account.Id);

        await _publisher.ApprovalDecidedAsync(_project, _deliverable, round, CancellationToken.None);

        Assert.Equal("elena2@example.com", Assert.IsType<ApprovalDecided>(Assert.Single(_events.Published)).ActorName);
    }

    [Fact]
    public async Task ApprovalDecided_IsSilent_WhenTheRequesterLeftTheWorkspace()
    {
        var account = _identity.AddAccount("elena2@example.com", "pw", "Elena");
        var round = Round(requestedBy: Guid.NewGuid());
        Decide(round, ApprovalStatus.Approved, account.Id);

        await _publisher.ApprovalDecidedAsync(_project, _deliverable, round, CancellationToken.None);

        Assert.Empty(_events.Published);
    }

    [Fact]
    public async Task InvitationAccepted_TellsTheMemberWhoInvited()
    {
        var account = _identity.AddAccount("elena2@example.com", "pw", "Elena Popescu");
        var invitation = Invitation(invitedBy: _member);

        await _publisher.InvitationAcceptedAsync(_workspace, _client, invitation, account.Id, CancellationToken.None);

        var sent = Assert.IsType<InvitationAccepted>(Assert.Single(_events.Published));
        Assert.Equal((_client.Id, "Elena Popescu"), (sent.ClientId, sent.ActorName));
        Assert.Equal((_member, "mihai@example.com"), (sent.RecipientUserId, sent.RecipientEmail));
    }

    [Fact]
    public async Task InvitationAccepted_IsSilent_WhenTheInviterLeftTheWorkspace()
    {
        var account = _identity.AddAccount("elena2@example.com", "pw", "Elena");

        await _publisher.InvitationAcceptedAsync(_workspace, _client, Invitation(invitedBy: Guid.NewGuid()), account.Id, CancellationToken.None);

        Assert.Empty(_events.Published);
    }

    [Fact]
    public async Task Withdrawn_SaysWhichRoundWasTakenBack()
    {
        var round = Guid.NewGuid();

        await _publisher.ApprovalRequestWithdrawnAsync(round, CancellationToken.None);

        Assert.Equal(new ApprovalRequestWithdrawn(round), Assert.Single(_events.Published));
    }

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
