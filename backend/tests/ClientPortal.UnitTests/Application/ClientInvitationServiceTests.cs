using ClientPortal.Application.Authorization;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Mappings;
using ClientPortal.Application.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.UnitTests.Application;

public class ClientInvitationServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeClientInvitationRepository _invitations = new();
    private readonly FakeIdentityService _identity = new();
    private readonly FakeEmailSender _email = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Workspace _workspace;
    private readonly Client _client;
    private readonly ClientInvitationService _service;

    public ClientInvitationServiceTests()
    {
        _identity.Accounts.Add(new(_me, "ana@studio.example", "Ana Pop"));
        _workspace = Workspace.Create("Studio Pop", _me);
        _workspaces.Items.Add(_workspace);
        _client = Client.Register(_workspace.Id, "Elena", "Elena@Acme.com");
        _clients.Items.Add(_client);

        _service = new ClientInvitationService(
            _clients,
            _invitations,
            _identity,
            _email,
            new FakeAppLinks(),
            new WorkspaceAccess(_workspaces, new FakeCurrentUser(_me)),
            new ClientResponseBuilder(_identity, _invitations, _time),
            _unitOfWork,
            _time);
    }

    [Fact]
    public async Task Invite_StoresOnlyTheHashOfTheToken_AndEmailsTheLinkToTheClientAddress()
    {
        var response = await _service.InviteAsync(_workspace.Id, _client.Id, CancellationToken.None);

        var invitation = Assert.Single(_invitations.Items);
        Assert.Equal("elena@acme.com", invitation.Email);
        Assert.Equal(_me, invitation.InvitedByUserId);
        Assert.Equal(_time.Now.UtcDateTime.AddDays(7), invitation.ExpiresAtUtc);

        var message = Assert.Single(_email.Sent);
        Assert.Equal("elena@acme.com", message.To);
        Assert.Contains("Studio Pop", message.Subject);
        Assert.Contains("Ana Pop", message.TextBody);
        var token = message.TextBody.Split("https://app.test/invite/")[1].Split(['\r', '\n'])[0];
        Assert.Equal(ClientInvitation.HashToken(token), invitation.TokenHash);
        Assert.DoesNotContain(token, invitation.TokenHash);

        Assert.Equal("Invited", response.Portal.Status);
        Assert.Equal(invitation.ExpiresAtUtc, response.Portal.ExpiresAtUtc);
        Assert.True(response.DetailsLocked);
    }

    [Fact]
    public async Task Invite_Again_CancelsThePreviousInvitation_SoOnlyTheNewLinkWorks()
    {
        await _service.InviteAsync(_workspace.Id, _client.Id, CancellationToken.None);
        var first = Assert.Single(_invitations.Items);

        await _service.InviteAsync(_workspace.Id, _client.Id, CancellationToken.None);

        Assert.Equal(2, _invitations.Items.Count);
        var second = _invitations.Items.Single(i => i != first);
        Assert.NotNull(first.CancelledAtUtc);
        Assert.Null(second.CancelledAtUtc);
        Assert.NotEqual(first.TokenHash, second.TokenHash);
        Assert.Equal(2, _email.Sent.Count);
    }

    [Fact]
    public async Task Invite_AfterTheFirstExpired_SendsAFreshInvitation()
    {
        await _service.InviteAsync(_workspace.Id, _client.Id, CancellationToken.None);
        _time.Now = _time.Now.AddDays(8);

        var response = await _service.InviteAsync(_workspace.Id, _client.Id, CancellationToken.None);

        Assert.Equal("Invited", response.Portal.Status);
        Assert.Equal(_time.Now.UtcDateTime.AddDays(7), response.Portal.ExpiresAtUtc);
    }

    [Fact]
    public async Task Invite_AnArchivedClient_ThrowsConflict_AndSendsNothing()
    {
        _client.Archive();

        await Assert.ThrowsAsync<ConflictException>(() => _service.InviteAsync(_workspace.Id, _client.Id, CancellationToken.None));

        Assert.Empty(_invitations.Items);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Invite_AClientWhoAlreadyHasAnAccount_ThrowsConflict()
    {
        _client.LinkTo(Guid.NewGuid(), _time.Now.UtcDateTime);

        await Assert.ThrowsAsync<ConflictException>(() => _service.InviteAsync(_workspace.Id, _client.Id, CancellationToken.None));

        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Invite_AsSomeoneWhoIsNotAMember_ThrowsNotFound()
    {
        var stranger = new ClientInvitationService(
            _clients,
            _invitations,
            _identity,
            _email,
            new FakeAppLinks(),
            new WorkspaceAccess(_workspaces, new FakeCurrentUser(Guid.NewGuid())),
            new ClientResponseBuilder(_identity, _invitations, _time),
            _unitOfWork,
            _time);

        await Assert.ThrowsAsync<NotFoundException>(() => stranger.InviteAsync(_workspace.Id, _client.Id, CancellationToken.None));

        Assert.Empty(_invitations.Items);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Invite_ClientOfAnotherWorkspace_ThrowsNotFound()
    {
        var other = Workspace.Create("Other", _me);
        _workspaces.Items.Add(other);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.InviteAsync(other.Id, _client.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_WithdrawsTheInvitation_AndUnlocksTheDetails()
    {
        await _service.InviteAsync(_workspace.Id, _client.Id, CancellationToken.None);

        var response = await _service.CancelAsync(_workspace.Id, _client.Id, CancellationToken.None);

        Assert.NotNull(Assert.Single(_invitations.Items).CancelledAtUtc);
        Assert.Equal("NotInvited", response.Portal.Status);
        Assert.False(response.DetailsLocked);
    }

    [Fact]
    public async Task Cancel_WithNothingToCancel_IsNotAnError()
    {
        var response = await _service.CancelAsync(_workspace.Id, _client.Id, CancellationToken.None);

        Assert.Equal("NotInvited", response.Portal.Status);
    }

    [Fact]
    public async Task Cancel_AClientWhoAlreadyAccepted_ThrowsConflict()
    {
        _client.LinkTo(Guid.NewGuid(), _time.Now.UtcDateTime);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CancelAsync(_workspace.Id, _client.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_AnExpiredInvitation_Works()
    {
        await _service.InviteAsync(_workspace.Id, _client.Id, CancellationToken.None);
        _time.Now = _time.Now.AddDays(8);

        var response = await _service.CancelAsync(_workspace.Id, _client.Id, CancellationToken.None);

        Assert.Equal("NotInvited", response.Portal.Status);
        Assert.Equal(InvitationStatus.Cancelled, Assert.Single(_invitations.Items).StatusAt(_time.Now.UtcDateTime));
    }
}
