using Partnr.Core.Application.Authorization;
using Partnr.Core.Application.DTOs.Members;
using Partnr.Core.Application.Exceptions;
using Partnr.Core.Application.Services;
using Partnr.Core.Domain.Entities;
using Partnr.Core.Domain.Enums;

namespace Partnr.Core.UnitTests.Application;

public class MemberInvitationServiceTests
{
    private readonly FakeIdentityService _identity = new();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeMemberInvitationRepository _invitations = new();
    private readonly FakeNotificationPublisher _notifications = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Guid _owner;
    private readonly Guid _member;
    private readonly Workspace _workspace;
    private readonly MemberInvitationService _service;

    public MemberInvitationServiceTests()
    {
        _owner = _identity.AddAccount("ana@studio.example", "pw", "Ana Pop").Id;
        _member = _identity.AddAccount("mihai@studio.example", "pw", "Mihai Member").Id;
        _workspace = Workspace.Create("Studio Pop", _owner);
        _workspace.AddMember(_member, WorkspaceRole.Member);
        _workspaces.Items.Add(_workspace);
        _currentUser.UserId = _owner;

        _service = new MemberInvitationService(
            _invitations,
            _clients,
            _identity,
            _notifications,
            new WorkspaceAccess(_workspaces, _currentUser),
            _unitOfWork,
            _time);
    }

    private static InviteMemberRequest To(string email) => new() { Email = email };

    [Fact]
    public async Task Invite_StoresOnlyTheHashOfTheToken_AndSendsTheTokenToTheAddress()
    {
        var response = await _service.InviteAsync(_workspace.Id, To(" New.Person@Example.com "), CancellationToken.None);

        var invitation = Assert.Single(_invitations.Items);
        Assert.Equal("new.person@example.com", invitation.Email);
        Assert.Equal(_owner, invitation.InvitedByUserId);
        Assert.Equal(_time.Now.UtcDateTime.AddDays(7), invitation.ExpiresAtUtc);

        Assert.Same(invitation, Assert.Single(_notifications.MemberInvitationsSent));
        Assert.Equal(MemberInvitation.HashToken(Assert.Single(_notifications.MemberInvitationTokens)), invitation.TokenHash);

        Assert.Equal((invitation.Id, "new.person@example.com", "Pending"), (response.Id, response.Email, response.Status));
    }

    [Fact]
    public async Task Invite_Again_CancelsThePreviousInvitation_SoOnlyTheNewLinkWorks()
    {
        await _service.InviteAsync(_workspace.Id, To("new@example.com"), CancellationToken.None);
        var first = Assert.Single(_invitations.Items);

        await _service.InviteAsync(_workspace.Id, To("NEW@example.com"), CancellationToken.None);

        Assert.Equal(2, _invitations.Items.Count);
        Assert.NotNull(first.CancelledAtUtc);
        Assert.Single(_invitations.Items, i => i.IsOpen);
    }

    [Fact]
    public async Task Invite_AMemberOfTheWorkspace_ThrowsConflict_AndSendsNothing()
    {
        await Assert.ThrowsAsync<ConflictException>(() => _service.InviteAsync(_workspace.Id, To("Mihai@Studio.example"), CancellationToken.None));

        Assert.Empty(_invitations.Items);
        Assert.Empty(_notifications.MemberInvitationsSent);
    }

    [Fact]
    public async Task Invite_AClientWithAnAccount_ThrowsConflict_ButAClientCardWithoutOneIsFine()
    {
        var elena = _identity.AddAccount("elena@acme.example", "pw", "Elena");
        var linked = Client.Register(_workspace.Id, "Elena", "elena@acme.example");
        linked.LinkTo(elena.Id, DateTime.UtcNow);
        _clients.Items.Add(linked);
        _clients.Items.Add(Client.Register(_workspace.Id, "Pat", "pat@acme.example"));

        await Assert.ThrowsAsync<ConflictException>(() => _service.InviteAsync(_workspace.Id, To("elena@acme.example"), CancellationToken.None));
        await _service.InviteAsync(_workspace.Id, To("pat@acme.example"), CancellationToken.None);

        Assert.Equal("pat@acme.example", Assert.Single(_invitations.Items).Email);
    }

    [Fact]
    public async Task Invite_AnAccountOfAnotherWorkspace_IsFine()
    {
        _identity.AddAccount("someone@else.example", "pw");

        await _service.InviteAsync(_workspace.Id, To("someone@else.example"), CancellationToken.None);

        Assert.Single(_invitations.Items);
    }

    [Fact]
    public async Task EveryActionIsTheOwnersOnly_AMemberGetsForbidden()
    {
        _currentUser.UserId = _member;

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.InviteAsync(_workspace.Id, To("x@example.com"), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.ListAsync(_workspace.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.CancelAsync(_workspace.Id, Guid.NewGuid(), CancellationToken.None));
        Assert.Empty(_invitations.Items);
    }

    [Fact]
    public async Task EveryActionIsNotFound_ForSomeoneOutsideTheWorkspace()
    {
        _currentUser.UserId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.InviteAsync(_workspace.Id, To("x@example.com"), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ListAsync(_workspace.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CancelAsync(_workspace.Id, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task List_ShowsTheOpenOnes_NewestFirst_AndMarksTheExpired()
    {
        await _service.InviteAsync(_workspace.Id, To("old@example.com"), CancellationToken.None);
        _time.Now = _time.Now.AddDays(3);
        await _service.InviteAsync(_workspace.Id, To("new@example.com"), CancellationToken.None);
        _invitations.Items.Add(AcceptedAlready());
        _time.Now = _time.Now.AddDays(5);

        var list = await _service.ListAsync(_workspace.Id, CancellationToken.None);

        Assert.Equal(["new@example.com", "old@example.com"], list.Select(i => i.Email));
        Assert.Equal(["Pending", "Expired"], list.Select(i => i.Status));
    }

    [Fact]
    public async Task Cancel_ClosesTheInvitation_AndCancellingAgainIsNotAnError()
    {
        var invitation = await _service.InviteAsync(_workspace.Id, To("new@example.com"), CancellationToken.None);

        await _service.CancelAsync(_workspace.Id, invitation.Id, CancellationToken.None);
        await _service.CancelAsync(_workspace.Id, invitation.Id, CancellationToken.None);

        Assert.NotNull(Assert.Single(_invitations.Items).CancelledAtUtc);
        Assert.Empty(await _service.ListAsync(_workspace.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_AnAcceptedOne_ThrowsConflict_AndAnUnknownOneNotFound()
    {
        var accepted = AcceptedAlready();
        _invitations.Items.Add(accepted);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CancelAsync(_workspace.Id, accepted.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CancelAsync(_workspace.Id, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_AnInvitationOfAnotherWorkspace_IsNotFound()
    {
        var other = Workspace.Create("Other", _member);
        _workspaces.Items.Add(other);
        var foreign = MemberInvitation.Create(other.Id, "x@example.com", "hash", _member, _time.Now.UtcDateTime);
        _invitations.Items.Add(foreign);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.CancelAsync(_workspace.Id, foreign.Id, CancellationToken.None));
        Assert.True(foreign.IsOpen);
    }

    private MemberInvitation AcceptedAlready()
    {
        var invitation = MemberInvitation.Create(_workspace.Id, "done@example.com", "done-hash", _owner, _time.Now.UtcDateTime);
        invitation.Accept(_time.Now.UtcDateTime);

        return invitation;
    }
}
