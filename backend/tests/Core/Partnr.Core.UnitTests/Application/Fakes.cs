using Partnr.Core.Application.DTOs.Auth;
using Partnr.Core.Application.Exceptions;
using Partnr.Core.Application.Interfaces.Repositories;
using Partnr.Core.Application.Interfaces.Services;
using Partnr.Core.Domain.Entities;
using Partnr.Core.Domain.Enums;

namespace Partnr.Core.UnitTests.Application;


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
        Items.Where(p => p.IsOpenToClient(Clients.Items.FirstOrDefault(c => c.Id == p.ClientId && c.UserId == userId)));

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

internal sealed class FakeMemberInvitationRepository : IMemberInvitationRepository
{
    public List<MemberInvitation> Items { get; } = [];

    public Task<MemberInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(i => i.TokenHash == tokenHash));

    public Task<MemberInvitation?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(i => i.WorkspaceId == workspaceId && i.Id == id));

    public Task<MemberInvitation?> GetOpenForEmailAsync(Guid workspaceId, string email, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(i => i.WorkspaceId == workspaceId && i.Email == email && i.IsOpen));

    public Task<IReadOnlyList<MemberInvitation>> ListOpenAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MemberInvitation>>(
            Items.Where(i => i.WorkspaceId == workspaceId && i.IsOpen).OrderByDescending(i => i.CreatedAtUtc).ToList());

    public void Add(MemberInvitation invitation) => Items.Add(invitation);
}

internal sealed class FakeAppLinks : IAppLinks
{
    public string InvitationUrl(string token) => $"https://app.test/invite/{token}";

    public string Url(string path) => $"https://app.test{path}";
}

internal sealed class FakeNotificationPublisher : INotificationPublisher
{
    public List<ApprovalRequest> Requested { get; } = [];

    public List<ApprovalRequest> Decided { get; } = [];

    public List<ClientInvitation> Accepted { get; } = [];

    public List<Guid> Withdrawn { get; } = [];

    public List<MemberInvitation> MemberInvitationsSent { get; } = [];

    public List<MemberInvitation> MembersJoined { get; } = [];

    public List<InvitationSent> Invited { get; } = [];

    public List<PasswordResetSent> PasswordResets { get; } = [];

    public Task ApprovalRequestedAsync(string workspaceName, Project project, Deliverable deliverable, ApprovalRequest round, CancellationToken cancellationToken)
    {
        Requested.Add(round);
        return Task.CompletedTask;
    }

    public Task ApprovalDecidedAsync(Project project, Deliverable deliverable, ApprovalRequest round, CancellationToken cancellationToken)
    {
        Decided.Add(round);
        return Task.CompletedTask;
    }

    public Task InvitationAcceptedAsync(Workspace workspace, Client client, ClientInvitation invitation, Guid accountId, CancellationToken cancellationToken)
    {
        Accepted.Add(invitation);
        return Task.CompletedTask;
    }

    public Task ClientInvitedAsync(string workspaceName, Client client, ClientInvitation invitation, string token, CancellationToken cancellationToken)
    {
        Invited.Add(new InvitationSent(workspaceName, client, invitation, token));
        return Task.CompletedTask;
    }

    public Task MemberInvitedAsync(string workspaceName, MemberInvitation invitation, string token, CancellationToken cancellationToken)
    {
        MemberInvitationsSent.Add(invitation);
        MemberInvitationTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task MemberJoinedAsync(Workspace workspace, MemberInvitation invitation, Guid accountId, CancellationToken cancellationToken)
    {
        MembersJoined.Add(invitation);
        return Task.CompletedTask;
    }

    public List<string> MemberInvitationTokens { get; } = [];

    public Task PasswordResetRequestedAsync(CurrentUserResponse account, string token, CancellationToken cancellationToken)
    {
        PasswordResets.Add(new PasswordResetSent(account, token));
        return Task.CompletedTask;
    }

    public Task ApprovalRequestWithdrawnAsync(Guid approvalRequestId, CancellationToken cancellationToken)
    {
        Withdrawn.Add(approvalRequestId);
        return Task.CompletedTask;
    }
}

internal sealed record InvitationSent(string WorkspaceName, Client Client, ClientInvitation Invitation, string Token);

internal sealed record PasswordResetSent(CurrentUserResponse Account, string Token);

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

internal sealed class FakeMeetingRepository : IMeetingRepository
{
    public List<Meeting> Items { get; } = [];

    public FakeClientRepository Clients { get; init; } = new();

    public Task<Meeting?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(m => m.WorkspaceId == workspaceId && m.Id == id));

    public Task<IReadOnlyList<Meeting>> ListAsync(Guid workspaceId, bool includeCancelled, Guid? clientId, Guid? projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Meeting>>(Items
            .Where(m => m.WorkspaceId == workspaceId
                && (includeCancelled || !m.IsCancelled)
                && (clientId is null || m.ClientId == clientId)
                && (projectId is null || m.ProjectId == projectId))
            .OrderBy(m => m.StartsAtUtc)
            .ToList());

    public Task<IReadOnlyList<Meeting>> ListForClientAccountAsync(Guid workspaceId, Guid userId, DateTime since, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Meeting>>(Items
            .Where(m => m.WorkspaceId == workspaceId
                && m.StartsAtUtc >= since
                && Clients.Items.Any(c => c.Id == m.ClientId && c.UserId == userId && !c.IsArchived))
            .OrderBy(m => m.StartsAtUtc)
            .ToList());

    public Task<Meeting?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(m => m.Id == id));

    public Task<IReadOnlyList<Meeting>> ListOpenUnsyncedForOrganizerAsync(Guid organizerUserId, DateTime nowUtc, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Meeting>>(Items
            .Where(m => m.OrganizerUserId == organizerUserId && m.EndsAtUtc > nowUtc && m.SyncStatus != MeetingSyncStatus.Synced)
            .ToList());

    public Task<int> CountUpcomingForOrganizerAsync(Guid workspaceId, Guid organizerUserId, DateTime nowUtc, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count(m => m.WorkspaceId == workspaceId && m.OrganizerUserId == organizerUserId && !m.IsCancelled && m.EndsAtUtc > nowUtc));

    public void Add(Meeting meeting) => Items.Add(meeting);
}

internal sealed class FakeConversationReadRepository : IConversationReadRepository
{
    public List<ConversationRead> Items { get; } = [];

    public Task<ConversationRead?> GetAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(r => r.UserId == userId && r.ProjectId == projectId));

    public void Add(ConversationRead read) => Items.Add(read);
}

internal sealed class FakeMessageRepository(FakeConversationReadRepository reads) : IMessageRepository
{
    public List<Message> Items { get; } = [];

    public Task<Message?> GetByIdAsync(Guid projectId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(m => m.ProjectId == projectId && m.Id == id));

    public Task<MessagePage> ListPageAsync(Guid projectId, Guid? beforeId, int limit, CancellationToken cancellationToken)
    {
        var all = Items.Where(m => m.ProjectId == projectId).OrderBy(m => m.CreatedAtUtc).ToList();
        if (beforeId is { } before && all.FindIndex(m => m.Id == before) is var at and >= 0)
        {
            all = all.Take(at).ToList();
        }

        var page = all.TakeLast(limit).ToList();

        return Task.FromResult(new MessagePage(page, all.Count > page.Count));
    }

    public Task<IReadOnlyDictionary<Guid, Message>> GetLatestPerProjectAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Message>>(Items
            .Where(m => projectIds.Contains(m.ProjectId))
            .GroupBy(m => m.ProjectId)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.CreatedAtUtc).Last()));

    public Task<IReadOnlyDictionary<Guid, int>> CountUnreadPerProjectAsync(
        Guid userId,
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        var counts = new Dictionary<Guid, int>();
        foreach (var projectId in projectIds)
        {
            var readUpTo = reads.Items.FirstOrDefault(r => r.UserId == userId && r.ProjectId == projectId)?.LastReadAtUtc ?? DateTime.MinValue;
            var unread = Items.Count(m => m.ProjectId == projectId && m.AuthorUserId != userId && !m.IsDeleted && m.CreatedAtUtc > readUpTo);
            if (unread > 0)
            {
                counts[projectId] = unread;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(counts);
    }

    public void Add(Message message) => Items.Add(message);
}

internal sealed class FakeGoogleConnectionStatus : IGoogleConnectionStatus
{
    /// <summary>The accounts that "have Google connected".</summary>
    public HashSet<Guid> Connected { get; } = [];

    /// <summary>Set to make the service "unreachable".</summary>
    public bool Down { get; set; }

    public Task<bool> IsConnectedAsync(Guid userId, CancellationToken cancellationToken) =>
        Down
            ? throw new ServiceUnavailableException("Integrations is down (fake).")
            : Task.FromResult(Connected.Contains(userId));
}
