using ClientPortal.Application.DTOs.Auth;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Application.Services;

namespace ClientPortal.UnitTests.Application;

public class AuthServiceTests
{
    private const string Password = "correct horse";

    private readonly FakeIdentityService _identity = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly AuthService _service;

    public AuthServiceTests() => _service = new AuthService(_identity, _currentUser);

    [Fact]
    public async Task Register_NewEmail_CreatesTrimmedAccountAndSignsIn()
    {
        var response = await _service.RegisterAsync(
            new RegisterRequest { Email = "  me@acme.com ", Password = Password, FullName = " Ana Pop " },
            CancellationToken.None);

        var account = Assert.Single(_identity.Accounts);
        Assert.Equal("me@acme.com", account.Email);
        Assert.Equal("Ana Pop", account.FullName);
        Assert.Equal(account.Id, response.Id);
        Assert.Equal(account, _identity.SignedIn);
    }

    [Fact]
    public async Task Register_ExistingEmail_ThrowsConflict_AndCreatesNothing()
    {
        _identity.Accounts.Add(new CurrentUserResponse(Guid.NewGuid(), "me@acme.com", "Existing"));

        await Assert.ThrowsAsync<ConflictException>(() => _service.RegisterAsync(
            new RegisterRequest { Email = "ME@acme.com", Password = Password, FullName = "Other" },
            CancellationToken.None));

        Assert.Single(_identity.Accounts);
        Assert.Null(_identity.SignedIn);
    }

    [Fact]
    public async Task Register_RejectedPassword_DoesNotSignIn()
    {
        _identity.RejectNewPasswords = true;

        await Assert.ThrowsAsync<ValidationFailedException>(() => _service.RegisterAsync(
            new RegisterRequest { Email = "me@acme.com", Password = Password, FullName = "Ana" },
            CancellationToken.None));

        Assert.Null(_identity.SignedIn);
    }

    [Fact]
    public async Task Login_CorrectPassword_ReturnsAccount()
    {
        var account = _identity.AddAccount("me@acme.com", Password);

        var response = await _service.LoginAsync(
            new LoginRequest { Email = "me@acme.com", Password = Password },
            CancellationToken.None);

        Assert.Equal(account.Id, response.Id);
        Assert.Equal(account, _identity.SignedIn);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_ThrowUnauthorizedWithTheSameMessage()
    {
        _identity.AddAccount("me@acme.com", Password);

        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(() => _service.LoginAsync(
            new LoginRequest { Email = "me@acme.com", Password = "wrong" },
            CancellationToken.None));
        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedException>(() => _service.LoginAsync(
            new LoginRequest { Email = "nobody@acme.com", Password = "wrong" },
            CancellationToken.None));

        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
        Assert.Null(_identity.SignedIn);
    }

    [Fact]
    public async Task Logout_SignsOut()
    {
        await _service.LogoutAsync();

        Assert.True(_identity.SignedOut);
    }

    [Fact]
    public async Task GetCurrentUser_Anonymous_ThrowsUnauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.GetCurrentUserAsync(CancellationToken.None));

    [Fact]
    public async Task GetCurrentUser_AccountDeletedMeanwhile_ThrowsUnauthorized()
    {
        _currentUser.UserId = Guid.NewGuid();

        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.GetCurrentUserAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentUser_SignedIn_ReturnsAccount()
    {
        var account = _identity.AddAccount("me@acme.com", Password);
        _currentUser.UserId = account.Id;

        var response = await _service.GetCurrentUserAsync(CancellationToken.None);

        Assert.Equal(account.Id, response.Id);
        Assert.Equal("me@acme.com", response.Email);
    }

    [Fact]
    public async Task UpdateProfile_TrimsTheNameAndStoresEmptyOptionalDetailsAsNull()
    {
        var account = _identity.AddAccount("me@acme.com", Password);
        _currentUser.UserId = account.Id;

        var response = await _service.UpdateProfileAsync(
            new UpdateProfileRequest { FullName = "  Ana Popescu ", Phone = " ", Company = " Acme SRL " },
            CancellationToken.None);

        Assert.Equal("Ana Popescu", response.FullName);
        Assert.Null(response.Phone);
        Assert.Equal("Acme SRL", response.Company);
        Assert.Equal("me@acme.com", response.Email);
        Assert.Equal(response, Assert.Single(_identity.Accounts));
    }

    [Fact]
    public async Task UpdateProfile_BlankName_ThrowsValidationFailed_AndChangesNothing()
    {
        var account = _identity.AddAccount("me@acme.com", Password, "Ana Pop");
        _currentUser.UserId = account.Id;

        await Assert.ThrowsAsync<ValidationFailedException>(() => _service.UpdateProfileAsync(
            new UpdateProfileRequest { FullName = "   ", Company = "Acme" },
            CancellationToken.None));

        Assert.Equal(account, Assert.Single(_identity.Accounts));
    }

    [Fact]
    public async Task UpdateProfile_Anonymous_ThrowsUnauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.UpdateProfileAsync(
            new UpdateProfileRequest { FullName = "Ana" },
            CancellationToken.None));

    [Fact]
    public async Task UpdateProfile_AccountDeletedMeanwhile_ThrowsUnauthorized()
    {
        _currentUser.UserId = Guid.NewGuid();

        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.UpdateProfileAsync(
            new UpdateProfileRequest { FullName = "Ana" },
            CancellationToken.None));
    }
}
