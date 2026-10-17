using ClientPortal.Application.DTOs.Auth;

namespace ClientPortal.Application.Interfaces.Services;

/// <summary>Accounts, passwords and the sign-in cookie, on top of ASP.NET Core Identity. It saves by itself, so it does not use IUnitOfWork.</summary>
public interface IIdentityService
{
    Task<CurrentUserResponse?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<CurrentUserResponse?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CurrentUserResponse>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Case-insensitive match on the start of the email; `%` and `_` are literal.</summary>
    Task<IReadOnlyList<CurrentUserResponse>> SearchByEmailStartAsync(
        string emailStart, int limit, IReadOnlyCollection<Guid> excludeIds, CancellationToken cancellationToken);

    Task ConfirmEmailAsync(Guid id, CancellationToken cancellationToken);

    Task<CurrentUserResponse> CreateAsync(string email, string fullName, string password, CancellationToken cancellationToken);

    /// <summary>Null when the account no longer exists.</summary>
    Task<CurrentUserResponse?> UpdateProfileAsync(Guid id, string fullName, string? phone, string? company, CancellationToken cancellationToken);

    Task SignInAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Null for an unknown email, a wrong password or a locked-out account.</summary>
    Task<CurrentUserResponse?> PasswordSignInAsync(string email, string password, CancellationToken cancellationToken);

    Task SignOutAsync();
}
