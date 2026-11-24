using Partnr.Identity.Application.DTOs.Auth;
using Partnr.Identity.Application.Exceptions;
using Partnr.Identity.Application.Interfaces.Repositories;
using Partnr.Identity.Application.Interfaces.Services;
using Partnr.Identity.Domain.Entities;

namespace Partnr.Identity.UnitTests.Application;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeEventPublisher : IEventPublisher
{
    public List<object> Published { get; } = [];

    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        Published.Add(@event);
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeCurrentUser(Guid? userId = null) : ICurrentUser
{
    public Guid? UserId { get; set; } = userId;
}

internal sealed class FakeTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakeIdentityService : IIdentityService
{
    private readonly Dictionary<Guid, string> _passwords = [];

    public List<CurrentUserResponse> Accounts { get; } = [];

    public CurrentUserResponse? SignedIn { get; private set; }

    public bool SignedOut { get; private set; }

    public bool RejectNewPasswords { get; set; }

    public HashSet<Guid> ConfirmedEmails { get; } = [];

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

    public Task<IReadOnlyList<CurrentUserResponse>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CurrentUserResponse>>(Accounts.Where(a => ids.Contains(a.Id)).ToList());

    public Task<IReadOnlyList<CurrentUserResponse>> SearchByEmailStartAsync(
        string emailStart, int limit, IReadOnlyCollection<Guid> excludeIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CurrentUserResponse>>(Accounts
            .Where(a => a.Email.StartsWith(emailStart, StringComparison.OrdinalIgnoreCase) && !excludeIds.Contains(a.Id))
            .OrderBy(a => a.Email, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList());

    public Task ConfirmEmailAsync(Guid id, CancellationToken cancellationToken)
    {
        ConfirmedEmails.Add(id);
        return Task.CompletedTask;
    }

    public Task<CurrentUserResponse> CreateAsync(string email, string fullName, string password, CancellationToken cancellationToken)
    {
        if (RejectNewPasswords)
        {
            throw new ValidationFailedException("Passwords must be at least 8 characters.");
        }

        return Task.FromResult(AddAccount(email, password, fullName));
    }

    public Task<CurrentUserResponse?> UpdateProfileAsync(
        Guid id, string fullName, string? phone, string? company, CancellationToken cancellationToken)
    {
        var index = Accounts.FindIndex(a => a.Id == id);
        if (index < 0)
        {
            return Task.FromResult<CurrentUserResponse?>(null);
        }

        Accounts[index] = Accounts[index] with { FullName = fullName, Phone = phone, Company = company };
        return Task.FromResult<CurrentUserResponse?>(Accounts[index]);
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

    public Dictionary<Guid, string> ResetPasswords { get; } = [];

    public Task<bool> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken)
    {
        if (RejectNewPasswords)
        {
            throw new ValidationFailedException("Passwords must be at least 8 characters.");
        }

        if (Accounts.All(a => a.Id != userId))
        {
            return Task.FromResult(false);
        }

        _passwords[userId] = newPassword;
        ResetPasswords[userId] = newPassword;
        return Task.FromResult(true);
    }
}

internal sealed class FakePasswordResetTokenRepository : IPasswordResetTokenRepository
{
    public List<PasswordResetToken> Items { get; } = [];

    public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<PasswordResetToken?> GetOpenForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(t => t.UserId == userId && t.IsOpen));

    public void Add(PasswordResetToken token) => Items.Add(token);
}
