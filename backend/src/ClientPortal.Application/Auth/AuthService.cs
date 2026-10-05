using ClientPortal.Application.Auth.Dtos.Requests;
using ClientPortal.Application.Auth.Dtos.Responses;
using ClientPortal.Application.Common;
using ClientPortal.Application.Common.Exceptions;

namespace ClientPortal.Application.Auth;

internal sealed class AuthService(IIdentityService identity, ICurrentUser currentUser) : IAuthService
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";
    private const string NotSignedInMessage = "Not signed in.";

    public async Task<CurrentUserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (await identity.FindByEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var account = await identity.CreateAsync(email, request.FullName.Trim(), request.Password, cancellationToken);
        await identity.SignInAsync(account.Id, cancellationToken);

        return account;
    }

    public async Task<CurrentUserResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        // Same message for unknown email, wrong password and lockout, so the response never reveals which emails exist.
        return await identity.PasswordSignInAsync(request.Email.Trim(), request.Password, cancellationToken)
            ?? throw new UnauthorizedException(InvalidCredentialsMessage);
    }

    public Task LogoutAsync() => identity.SignOutAsync();

    public async Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedException(NotSignedInMessage);
        }

        // The account may have been deleted while its cookie is still valid.
        return await identity.FindByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedException(NotSignedInMessage);
    }
}
