using ClientPortal.Application.DTOs.Auth;

namespace ClientPortal.Application.Interfaces.Services;

/// <summary>
/// Accounts, passwords and the sign-in cookie. Implemented in Infrastructure on top of ASP.NET Core Identity, which owns
/// password hashing, lockout and the cookie, and saves by itself (so this is not a repository and does not use IUnitOfWork).
/// </summary>
public interface IIdentityService
{
    Task<CurrentUserResponse?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<CurrentUserResponse?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The accounts with these ids (those that exist), in no particular order.</summary>
    Task<IReadOnlyList<CurrentUserResponse>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Accounts whose email starts with the text (case-insensitive; the text is matched literally, `%` and `_` are not wildcards),
    /// ordered by email, at most `limit`, skipping the given accounts.
    /// </summary>
    Task<IReadOnlyList<CurrentUserResponse>> SearchByEmailStartAsync(
        string emailStart, int limit, IReadOnlyCollection<Guid> excludeIds, CancellationToken cancellationToken);

    /// <summary>Marks the account's email as confirmed: the person proved they own it by using a link sent there.</summary>
    Task ConfirmEmailAsync(Guid id, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.ValidationFailedException">The password does not meet the policy.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The email is already used (lost a race with a concurrent registration).</exception>
    Task<CurrentUserResponse> CreateAsync(string email, string fullName, string password, CancellationToken cancellationToken);

    /// <summary>Saves the profile details (already trimmed; optional ones are null when empty). Null when the account no longer exists.</summary>
    Task<CurrentUserResponse?> UpdateProfileAsync(Guid id, string fullName, string? phone, string? company, CancellationToken cancellationToken);

    Task SignInAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Checks the password (counting failures towards lockout) and signs in. Null for an unknown email, a wrong password or a locked-out account.</summary>
    Task<CurrentUserResponse?> PasswordSignInAsync(string email, string password, CancellationToken cancellationToken);

    Task SignOutAsync();
}
