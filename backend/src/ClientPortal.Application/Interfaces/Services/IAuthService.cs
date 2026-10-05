using ClientPortal.Application.DTOs.Auth;

namespace ClientPortal.Application.Interfaces.Services;

public interface IAuthService
{
    /// <summary>Creates the account and signs it in.</summary>
    /// <exception cref="Common.Exceptions.ConflictException">The email is already used.</exception>
    /// <exception cref="Common.Exceptions.ValidationFailedException">The password does not meet the policy.</exception>
    Task<CurrentUserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.UnauthorizedException">Wrong credentials or locked-out account (deliberately indistinguishable).</exception>
    Task<CurrentUserResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task LogoutAsync();

    /// <exception cref="Common.Exceptions.UnauthorizedException">There is no signed-in user.</exception>
    Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken);
}
