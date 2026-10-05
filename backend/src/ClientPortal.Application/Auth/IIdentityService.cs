using ClientPortal.Application.Auth.Dtos.Responses;

namespace ClientPortal.Application.Auth;

/// <summary>
/// Accounts, passwords and the sign-in cookie. Implemented in Infrastructure on top of ASP.NET Core Identity, which owns
/// password hashing, lockout and the cookie, and saves by itself (so this is not a repository and does not use IUnitOfWork).
/// </summary>
public interface IIdentityService
{
    Task<CurrentUserResponse?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<CurrentUserResponse?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.ValidationFailedException">The password does not meet the policy.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The email is already used (lost a race with a concurrent registration).</exception>
    Task<CurrentUserResponse> CreateAsync(string email, string fullName, string password, CancellationToken cancellationToken);

    Task SignInAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Checks the password (counting failures towards lockout) and signs in. Null for an unknown email, a wrong password or a locked-out account.</summary>
    Task<CurrentUserResponse?> PasswordSignInAsync(string email, string password, CancellationToken cancellationToken);

    Task SignOutAsync();
}
