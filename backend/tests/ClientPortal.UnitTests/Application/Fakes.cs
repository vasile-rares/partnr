using ClientPortal.Application.DTOs.Auth;
using ClientPortal.Application.DTOs.Email;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.UnitTests.Application;


internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

internal sealed class FakeCurrentUser(Guid? userId = null) : ICurrentUser
{
    public Guid? UserId { get; set; } = userId;
}

internal sealed class FakeWorkspaceRepository : IWorkspaceRepository
{
    public List<Workspace> Items { get; } = [];

    public Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(w => w.Id == id));

    public Task<IReadOnlyList<Workspace>> ListForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Workspace>>(Items.Where(w => w.RoleOf(userId) is not null).ToList());

    public Dictionary<Guid, List<Guid>> ClientOf { get; } = [];

    public Task<IReadOnlyList<Workspace>> ListForClientAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Workspace>>(
            Items.Where(w => ClientOf.TryGetValue(userId, out var ids) && ids.Contains(w.Id)).ToList());

    public void Add(Workspace workspace) => Items.Add(workspace);
}

internal sealed class FakeClientRepository : IClientRepository
{
    public List<Client> Items { get; } = [];

    public Task<Client?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(c => c.WorkspaceId == workspaceId && c.Id == id));

    public Task<IReadOnlyList<Client>> ListAsync(Guid workspaceId, bool includeArchived, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Client>>(Items
            .Where(c => c.WorkspaceId == workspaceId && (includeArchived || !c.IsArchived))
            .OrderBy(c => c.Name)
            .ToList());

    public Task<IReadOnlyList<Client>> ListByIdsAsync(Guid workspaceId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Client>>(Items.Where(c => c.WorkspaceId == workspaceId && ids.Contains(c.Id)).ToList());

    public Task<bool> EmailExistsAsync(Guid workspaceId, string email, Guid? exceptClientId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(c => c.WorkspaceId == workspaceId && c.Email == email && c.Id != exceptClientId));

    public Task<IReadOnlySet<string>> ExistingEmailsAsync(Guid workspaceId, IReadOnlyCollection<string> emails, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<string>>(Items
            .Where(c => c.WorkspaceId == workspaceId && emails.Contains(c.Email))
            .Select(c => c.Email)
            .ToHashSet());

    public Task<bool> IsLinkedAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(c => c.WorkspaceId == workspaceId && c.UserId == userId));

    public void Add(Client client) => Items.Add(client);
}

internal sealed class FakeProjectRepository : IProjectRepository
{
    public List<Project> Items { get; } = [];

    public Task<Project?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(p => p.WorkspaceId == workspaceId && p.Id == id));

    public Task<IReadOnlyList<Project>> ListAsync(Guid workspaceId, bool includeArchived, Guid? clientId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Project>>(Items
            .Where(p => p.WorkspaceId == workspaceId && (includeArchived || !p.IsArchived) && (clientId is null || p.ClientId == clientId))
            .ToList());

    public FakeClientRepository Clients { get; init; } = new();

    private IEnumerable<Project> SharedWith(Guid userId) =>
        Items.Where(p => !p.IsArchived
            && p.Status != ProjectStatus.Planning
            && Clients.Items.Any(c => c.Id == p.ClientId && c.UserId == userId && !c.IsArchived));

    public Task<IReadOnlyList<Project>> ListSharedWithClientAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Project>>(SharedWith(userId).ToList());

    public Task<Project?> GetSharedWithClientAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(SharedWith(userId).FirstOrDefault(p => p.Id == id));

    public void Add(Project project) => Items.Add(project);
}

internal sealed class FakeDeliverableRepository : IDeliverableRepository
{
    public List<Deliverable> Items { get; } = [];

    public Task<Deliverable?> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(d => d.WorkspaceId == workspaceId && d.ProjectId == projectId && d.Id == id));

    public Task<IReadOnlyList<Deliverable>> ListAsync(Guid workspaceId, Guid projectId, bool includeArchived, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Deliverable>>(Items
            .Where(d => d.WorkspaceId == workspaceId && d.ProjectId == projectId && (includeArchived || !d.IsArchived))
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToList());

    public Task<IReadOnlyList<Deliverable>> ListSharedAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Deliverable>>(Items
            .Where(d => d.ProjectId == projectId && d.Status == DeliverableStatus.Shared && !d.IsArchived)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToList());

    public void Add(Deliverable deliverable) => Items.Add(deliverable);

    public void Remove(Deliverable deliverable) => Items.Remove(deliverable);
}

internal sealed class FakeClientInvitationRepository : IClientInvitationRepository
{
    public List<ClientInvitation> Items { get; } = [];

    public Task<ClientInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(i => i.TokenHash == tokenHash));

    public Task<ClientInvitation?> GetOpenForClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(i => i.ClientId == clientId && i.IsOpen));

    public Task<IReadOnlyList<ClientInvitation>> ListOpenForClientsAsync(IReadOnlyCollection<Guid> clientIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ClientInvitation>>(Items.Where(i => clientIds.Contains(i.ClientId) && i.IsOpen).ToList());

    public void Add(ClientInvitation invitation) => Items.Add(invitation);
}

internal sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class FakeAppLinks : IAppLinks
{
    public string InvitationUrl(string token) => $"https://app.test/invite/{token}";
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
}
