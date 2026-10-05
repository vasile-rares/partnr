using ClientPortal.Application.Auth;
using ClientPortal.Application.Auth.Dtos.Requests;
using ClientPortal.Application.Auth.Dtos.Responses;
using ClientPortal.Application.Common;
using ClientPortal.Application.Common.Exceptions;

namespace ClientPortal.Tests.Application;

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

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly Dictionary<Guid, string> _passwords = [];

        public List<CurrentUserResponse> Accounts { get; } = [];

        public CurrentUserResponse? SignedIn { get; private set; }

        public bool SignedOut { get; private set; }

        public bool RejectNewPasswords { get; set; }

        public CurrentUserResponse AddAccount(string email, string password, string fullName = "Test User")
        {
            var account = new CurrentUserResponse(Guid.NewGuid(), email, fullName);
            Accounts.Add(account);
            _passwords[account.Id] = password;
            return account;
        }

        public Task<CurrentUserResponse?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Accounts.FirstOrDefault(a => string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase)));

        public Task<CurrentUserResponse?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Accounts.FirstOrDefault(a => a.Id == id));

        public Task<CurrentUserResponse> CreateAsync(string email, string fullName, string password, CancellationToken cancellationToken)
        {
            if (RejectNewPasswords)
            {
                throw new ValidationFailedException("Passwords must be at least 8 characters.");
            }

            return Task.FromResult(AddAccount(email, password, fullName));
        }

        public Task SignInAsync(Guid userId, CancellationToken cancellationToken)
        {
            SignedIn = Accounts.Single(a => a.Id == userId);
            return Task.CompletedTask;
        }

        public Task<CurrentUserResponse?> PasswordSignInAsync(string email, string password, CancellationToken cancellationToken)
        {
            var account = Accounts.FirstOrDefault(a => string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase));
            if (account is null || _passwords[account.Id] != password)
            {
                return Task.FromResult<CurrentUserResponse?>(null);
            }

            SignedIn = account;
            return Task.FromResult<CurrentUserResponse?>(account);
        }

        public Task SignOutAsync()
        {
            SignedOut = true;
            return Task.CompletedTask;
        }
    }
}
