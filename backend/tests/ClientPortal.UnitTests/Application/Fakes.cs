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

internal sealed class FakeApprovalRequestRepository : IApprovalRequestRepository
{
    public List<ApprovalRequest> Items { get; } = [];

    public Task<ApprovalRequest?> GetByIdAsync(Guid workspaceId, Guid projectId, Guid deliverableId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(a =>
            a.WorkspaceId == workspaceId && a.ProjectId == projectId && a.DeliverableId == deliverableId && a.Id == id));

    public Task<ApprovalRequest?> GetPendingAsync(Guid deliverableId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(a => a.DeliverableId == deliverableId && a.IsPending));

    public Task<bool> AnyAsync(Guid deliverableId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(a => a.DeliverableId == deliverableId));

    public Task<IReadOnlyList<ApprovalRequest>> ListForDeliverableAsync(Guid workspaceId, Guid projectId, Guid deliverableId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ApprovalRequest>>(NewestFirst(
            Items.Where(a => a.WorkspaceId == workspaceId && a.ProjectId == projectId && a.DeliverableId == deliverableId)));

    public Task<IReadOnlyList<ApprovalRequest>> ListForProjectAsync(Guid workspaceId, Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ApprovalRequest>>(NewestFirst(Items.Where(a => a.WorkspaceId == workspaceId && a.ProjectId == projectId)));

    // The real query also joins the deliverable (shared and not archived); the services keep that true for every pending round.
    public Task<IReadOnlyDictionary<Guid, int>> CountPendingForClientAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(Items
            .Where(a => a.IsPending && projectIds.Contains(a.ProjectId))
            .GroupBy(a => a.ProjectId)
            .ToDictionary(g => g.Key, g => g.Count()));

    // The real query also joins the deliverable (shared and not archived); the unit tests that use it only have such deliverables.
    public Task<IReadOnlyDictionary<Guid, ApprovalCounts>> CountForProjectsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, ApprovalCounts>>(NewestFirst(Items.Where(a => projectIds.Contains(a.ProjectId)))
            .Where(a => a.Status != ApprovalStatus.Withdrawn)
            .GroupBy(a => a.DeliverableId)
            .Select(g => g.First())
            .Where(a => a.Status is ApprovalStatus.Pending or ApprovalStatus.ChangesRequested)
            .GroupBy(a => a.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => new ApprovalCounts(g.Count(a => a.IsPending), g.Count(a => a.Status == ApprovalStatus.ChangesRequested))));

    public void Add(ApprovalRequest request) => Items.Add(request);

    // Reversed first: rounds added within the same tick keep the later one in front, like the real ordering by id.
    private static List<ApprovalRequest> NewestFirst(IEnumerable<ApprovalRequest> rounds) =>
        rounds.Reverse().OrderByDescending(a => a.RequestedAtUtc).ToList();
}

internal sealed class FakeProjectUpdateRepository : IProjectUpdateRepository
{
    public List<ProjectUpdate> Items { get; } = [];

    public Task<ProjectUpdate?> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(u => u.WorkspaceId == workspaceId && u.ProjectId == projectId && u.Id == id));

    public Task<IReadOnlyList<ProjectUpdate>> ListAsync(Guid workspaceId, Guid projectId, bool includeArchived, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectUpdate>>(Items
            .Where(u => u.WorkspaceId == workspaceId && u.ProjectId == projectId && (includeArchived || !u.IsArchived))
            .OrderByDescending(u => u.CreatedAtUtc)
            .ToList());

    public Task<IReadOnlyList<ProjectUpdate>> ListSharedAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectUpdate>>(Items
            .Where(u => u.ProjectId == projectId && u.Status == ProjectUpdateStatus.Shared && !u.IsArchived)
            .OrderByDescending(u => u.SharedAtUtc)
            .ToList());

    public void Add(ProjectUpdate update) => Items.Add(update);

    public void Remove(ProjectUpdate update) => Items.Remove(update);
}

internal sealed class FakeProjectFileRepository : IProjectFileRepository
{
    public List<ProjectFile> Items { get; } = [];

    public Task<ProjectFile?> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(f => f.WorkspaceId == workspaceId && f.ProjectId == projectId && f.Id == id));

    public Task<IReadOnlyList<ProjectFile>> ListAsync(Guid workspaceId, Guid projectId, bool includeArchived, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectFile>>(Items
            .Where(f => f.WorkspaceId == workspaceId && f.ProjectId == projectId && (includeArchived || !f.IsArchived))
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToList());

    public Task<IReadOnlyList<ProjectFile>> ListSharedAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectFile>>(Items
            .Where(f => f.ProjectId == projectId && f.Status == ProjectFileStatus.Shared && !f.IsArchived)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToList());

    public void Add(ProjectFile file) => Items.Add(file);

    public void Remove(ProjectFile file) => Items.Remove(file);
}

internal sealed class FakeFileStorage : IFileStorage
{
    public Dictionary<string, byte[]> Objects { get; } = [];

    public Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        content.CopyTo(copy);
        Objects[key] = copy.ToArray();
        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        Objects.TryGetValue(key, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes))
            : throw new NotFoundException("The file's content was not found.");

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        Objects.Remove(key);
        return Task.CompletedTask;
    }
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
