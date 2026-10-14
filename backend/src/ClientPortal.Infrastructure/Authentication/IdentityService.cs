using ClientPortal.Application.DTOs.Auth;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

    public async Task<IReadOnlyList<CurrentUserResponse>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.ToArray();
        var users = await userManager.Users.AsNoTracking().Where(u => wanted.Contains(u.Id)).ToListAsync(cancellationToken);

        return users.Select(u => ToResponse(u)!).ToList();
    }

    public async Task<IReadOnlyList<CurrentUserResponse>> SearchByEmailStartAsync(
        string emailStart, int limit, IReadOnlyCollection<Guid> excludeIds, CancellationToken cancellationToken)
    {
        // NormalizedEmail is stored upper-cased. StartsWith becomes LIKE with the text escaped, so a typed % or _ is literal.
        var start = emailStart.ToUpperInvariant();
        var excluded = excludeIds.ToArray();
        var users = await userManager.Users
            .AsNoTracking()
            .Where(u => u.NormalizedEmail != null && u.NormalizedEmail.StartsWith(start) && !excluded.Contains(u.Id))
            .OrderBy(u => u.NormalizedEmail)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return users.Select(u => ToResponse(u)!).ToList();
    }

    public async Task ConfirmEmailAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || user.EmailConfirmed)
        {
            return;
        }

        user.EmailConfirmed = true;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not confirm the email of account '{id}': {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }
    }

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

    public async Task<CurrentUserResponse?> UpdateProfileAsync(
        Guid id, string fullName, string? phone, string? company, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return null;
        }

        user.FullName = fullName;
        user.PhoneNumber = phone;
        user.Company = company;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new ValidationFailedException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        return ToResponse(user);
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
        user is null ? null : new(user.Id, user.Email ?? string.Empty, user.FullName, user.PhoneNumber, user.Company);
}
