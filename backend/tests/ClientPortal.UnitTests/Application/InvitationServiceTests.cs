using ClientPortal.Application.DTOs.Invitations;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.UnitTests.Application;

public class InvitationServiceTests
{
    private const string Token = "a-test-token";
    private const string InvitedEmail = "elena@acme.com";

    private readonly Guid _owner = Guid.NewGuid();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeClientInvitationRepository _invitations = new();
    private readonly FakeIdentityService _identity = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Workspace _workspace;
    private readonly Client _client;
    private readonly ClientInvitation _invitation;
    private readonly InvitationService _service;

    public InvitationServiceTests()
    {
        _workspace = Workspace.Create("Studio Pop", _owner);
        _workspaces.Items.Add(_workspace);
        _client = Client.Register(_workspace.Id, "Elena", InvitedEmail);
        _clients.Items.Add(_client);
        _invitation = ClientInvitation.Create(
            _workspace.Id, _client.Id, InvitedEmail, ClientInvitation.HashToken(Token), _owner, _time.Now.UtcDateTime);
        _invitations.Items.Add(_invitation);

        _service = new InvitationService(_invitations, _clients, _workspaces, _identity, _currentUser, _unitOfWork, _time);
    }

    [Fact]
    public async Task Get_ShowsTheWorkspaceTheAddressAndWhetherAnAccountExists()
    {
        _identity.AddAccount(InvitedEmail, "correct horse");

        var preview = await _service.GetAsync(Token, CancellationToken.None);

        Assert.Equal("Studio Pop", preview.WorkspaceName);
        Assert.Equal(InvitedEmail, preview.Email);
        Assert.Equal("Pending", preview.Status);
        Assert.True(preview.AccountExists);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-token")]
    public async Task Get_WithAnUnknownToken_ThrowsNotFound(string token) =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(token, CancellationToken.None));

    [Fact]
    public async Task Get_WithAnAbsurdlyLongToken_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetAsync(new string('a', ClientInvitation.TokenMaxLength + 1), CancellationToken.None));

    [Fact]
    public async Task Get_AfterExpiry_ReportsExpired()
    {
        _time.Now = _time.Now.AddDays(8);

        var preview = await _service.GetAsync(Token, CancellationToken.None);

        Assert.Equal("Expired", preview.Status);
    }

    [Fact]
    public async Task Accept_AsTheInvitedAccount_LinksTheClientAndConfirmsTheEmail()
    {
        var account = _identity.AddAccount("ELENA@acme.com", "correct horse");
        _currentUser.UserId = account.Id;

        var accepted = await _service.AcceptAsync(Token, CancellationToken.None);

        Assert.Equal(_workspace.Id, accepted.WorkspaceId);
        Assert.Equal(account.Id, _client.UserId);
        Assert.NotNull(_invitation.AcceptedAtUtc);
        Assert.Contains(account.Id, _identity.ConfirmedEmails);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Accept_Anonymous_ThrowsUnauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.AcceptAsync(Token, CancellationToken.None));

    [Fact]
    public async Task Accept_AsAnAccountWithAnotherEmail_ThrowsForbidden_AndChangesNothing()
    {
        var account = _identity.AddAccount("someone.else@example.com", "correct horse");
        _currentUser.UserId = account.Id;

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.AcceptAsync(Token, CancellationToken.None));

        Assert.False(_client.IsLinked);
        Assert.Null(_invitation.AcceptedAtUtc);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Accept_AsAMemberOfTheWorkspace_ThrowsConflict()
    {
        var account = _identity.AddAccount(InvitedEmail, "correct horse");
        _workspace.AddMember(account.Id, WorkspaceRole.Member);
        _currentUser.UserId = account.Id;

        await Assert.ThrowsAsync<ConflictException>(() => _service.AcceptAsync(Token, CancellationToken.None));

        Assert.False(_client.IsLinked);
    }

    [Fact]
    public async Task Accept_AnAccountThatIsAlreadyAClientInThisWorkspace_ThrowsConflict()
    {
        var account = _identity.AddAccount(InvitedEmail, "correct horse");
        var existing = Client.Register(_workspace.Id, "Elena (other card)", "other@acme.com");
        existing.LinkTo(account.Id, _time.Now.UtcDateTime);
        _clients.Items.Add(existing);
        _currentUser.UserId = account.Id;

        await Assert.ThrowsAsync<ConflictException>(() => _service.AcceptAsync(Token, CancellationToken.None));

        Assert.False(_client.IsLinked);
    }

    [Fact]
    public async Task Accept_OnlyOnce()
    {
        var account = _identity.AddAccount(InvitedEmail, "correct horse");
        _currentUser.UserId = account.Id;
        await _service.AcceptAsync(Token, CancellationToken.None);

        var second = await Assert.ThrowsAsync<ConflictException>(() => _service.AcceptAsync(Token, CancellationToken.None));

        Assert.Contains("already used", second.Message);
    }

    [Theory]
    [InlineData(8, "expired")]
    [InlineData(0, "cancelled")]
    public async Task Accept_AnExpiredOrCancelledInvitation_ThrowsConflict(int daysLater, string expected)
    {
        if (expected == "cancelled")
        {
            _invitation.Cancel(_time.Now.UtcDateTime);
        }

        _time.Now = _time.Now.AddDays(daysLater);
        var account = _identity.AddAccount(InvitedEmail, "correct horse");
        _currentUser.UserId = account.Id;

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.AcceptAsync(Token, CancellationToken.None));

        Assert.Contains(expected, ex.Message);
        Assert.False(_client.IsLinked);
    }

    [Fact]
    public async Task Register_CreatesTheAccountWithTheInvitedEmail_LinksTheClientAndSignsIn()
    {
        var accepted = await _service.RegisterAsync(
            Token,
            new RegisterViaInvitationRequest { FullName = " Elena Ionescu ", Password = "correct horse", Phone = " 0700 ", Company = " " },
            CancellationToken.None);

        var account = Assert.Single(_identity.Accounts);
        Assert.Equal(InvitedEmail, account.Email);
        Assert.Equal("Elena Ionescu", account.FullName);
        Assert.Equal("0700", account.Phone);
        Assert.Null(account.Company);
        Assert.Equal(account.Id, _client.UserId);
        Assert.Equal(account, _identity.SignedIn);
        Assert.Contains(account.Id, _identity.ConfirmedEmails);
        Assert.Equal(_workspace.Id, accepted.WorkspaceId);
        Assert.NotNull(_invitation.AcceptedAtUtc);
    }

    [Fact]
    public async Task Register_WhenAnAccountWithThatEmailExists_ThrowsConflict_AndCreatesNothing()
    {
        _identity.AddAccount(InvitedEmail, "correct horse");

        await Assert.ThrowsAsync<ConflictException>(() => _service.RegisterAsync(
            Token,
            new RegisterViaInvitationRequest { FullName = "Elena", Password = "another password" },
            CancellationToken.None));

        Assert.Single(_identity.Accounts);
        Assert.False(_client.IsLinked);
        Assert.Null(_identity.SignedIn);
    }

    [Fact]
    public async Task Register_WithARejectedPassword_LeavesTheInvitationUsable()
    {
        _identity.RejectNewPasswords = true;

        await Assert.ThrowsAsync<ValidationFailedException>(() => _service.RegisterAsync(
            Token,
            new RegisterViaInvitationRequest { FullName = "Elena", Password = "short" },
            CancellationToken.None));

        Assert.Empty(_identity.Accounts);
        Assert.Null(_invitation.AcceptedAtUtc);
        Assert.Null(_identity.SignedIn);
    }

    [Fact]
    public async Task Register_AnExpiredInvitation_ThrowsConflict_AndCreatesNothing()
    {
        _time.Now = _time.Now.AddDays(8);

        await Assert.ThrowsAsync<ConflictException>(() => _service.RegisterAsync(
            Token,
            new RegisterViaInvitationRequest { FullName = "Elena", Password = "correct horse" },
            CancellationToken.None));

        Assert.Empty(_identity.Accounts);
    }
}
