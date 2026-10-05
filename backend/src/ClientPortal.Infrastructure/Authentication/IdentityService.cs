using ClientPortal.Application.DTOs.Auth;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Services;
using Microsoft.AspNetCore.Identity;

namespace ClientPortal.Infrastructure.Authentication;

/// <summary>Adapter from <see cref="IIdentityService"/> to ASP.NET Core Identity; no business rules live here.</summary>
internal sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : IIdentityService
{
    public async Task<CurrentUserResponse?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        ToResponse(await userManager.FindByEmailAsync(email));

    public async Task<CurrentUserResponse?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await userManager.FindByIdAsync(id.ToString()));

    public async Task<CurrentUserResponse> CreateAsync(string email, string fullName, string password, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new ValidationFailedException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        return ToResponse(user)!;
    }

    public async Task SignInAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"Account '{userId}' disappeared before sign-in.");

        await signInManager.SignInAsync(user, isPersistent: true);
    }

    public async Task<CurrentUserResponse?> PasswordSignInAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return null;
        }

        var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);

        return result.Succeeded ? ToResponse(user) : null;
    }

    public Task SignOutAsync() => signInManager.SignOutAsync();

    private static CurrentUserResponse? ToResponse(ApplicationUser? user) =>
        user is null ? null : new(user.Id, user.Email ?? string.Empty, user.FullName);
}
