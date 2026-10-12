using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Services;
using ClientPortal.Domain.Entities;

namespace ClientPortal.UnitTests.Application;

public class ClientServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Workspace _workspace;
    private readonly ClientService _service;

    public ClientServiceTests()
    {
        _workspace = Workspace.Create("Studio", _me);
        _workspaces.Items.Add(_workspace);
        _service = new ClientService(_clients, new WorkspaceAccess(_workspaces, new FakeCurrentUser(_me)), _unitOfWork);
    }

    [Fact]
    public async Task Register_NewEmail_AddsClientToTheWorkspaceAndSavesOnce()
    {
        var response = await _service.RegisterAsync(
            _workspace.Id,
            new RegisterClientRequest { Name = "Acme", Email = "Contact@Acme.com" },
            CancellationToken.None);

        Assert.Equal("contact@acme.com", response.Email);
        var client = Assert.Single(_clients.Items);
        Assert.Equal(_workspace.Id, client.WorkspaceId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Register_ExistingEmailInTheSameWorkspace_ThrowsConflict_AndDoesNotSave()
    {
        _clients.Items.Add(Client.Register(_workspace.Id, "Acme", "contact@acme.com"));

        await Assert.ThrowsAsync<ConflictException>(() => _service.RegisterAsync(
            _workspace.Id,
            new RegisterClientRequest { Name = "Other", Email = "CONTACT@acme.com" },
            CancellationToken.None));

        Assert.Single(_clients.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Register_EmailUsedInAnotherWorkspace_IsAllowed()
    {
        var other = Workspace.Create("Other studio", Guid.NewGuid());
        _workspaces.Items.Add(other);
        _clients.Items.Add(Client.Register(other.Id, "Acme", "contact@acme.com"));

        await _service.RegisterAsync(
            _workspace.Id,
            new RegisterClientRequest { Name = "Acme", Email = "contact@acme.com" },
            CancellationToken.None);

        Assert.Equal(2, _clients.Items.Count);
    }

    [Fact]
    public async Task Register_InAWorkspaceIAmNotAMemberOf_ThrowsNotFound_AndDoesNotSave()
    {
        var someoneElses = Workspace.Create("Not mine", Guid.NewGuid());
        _workspaces.Items.Add(someoneElses);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.RegisterAsync(
            someoneElses.Id,
            new RegisterClientRequest { Name = "Acme", Email = "contact@acme.com" },
            CancellationToken.None));

        Assert.Empty(_clients.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetById_Existing_ReturnsClient()
    {
        var client = Client.Register(_workspace.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(client);

        var response = await _service.GetByIdAsync(_workspace.Id, client.Id, CancellationToken.None);

        Assert.Equal(client.Id, response.Id);
    }

    [Fact]
    public async Task GetById_Missing_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetByIdAsync(_workspace.Id, Guid.NewGuid(), CancellationToken.None));

    [Fact]
    public async Task GetById_ClientOfAnotherWorkspace_ThrowsNotFound_EvenWithItsId()
    {
        var other = Workspace.Create("Other studio", Guid.NewGuid());
        _workspaces.Items.Add(other);
        var theirs = Client.Register(other.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetByIdAsync(_workspace.Id, theirs.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetById_InAWorkspaceIAmNotAMemberOf_ThrowsNotFound()
    {
        var someoneElses = Workspace.Create("Not mine", Guid.NewGuid());
        _workspaces.Items.Add(someoneElses);
        var theirs = Client.Register(someoneElses.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetByIdAsync(someoneElses.Id, theirs.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Register_KeepsTheOptionalDetails()
    {
        var response = await _service.RegisterAsync(
            _workspace.Id,
            new RegisterClientRequest { Name = "Acme", Email = "contact@acme.com", Company = "Acme Group", Phone = "0700", Notes = " " },
            CancellationToken.None);

        Assert.Equal("Acme Group", response.Company);
        Assert.Equal("0700", response.Phone);
        Assert.Null(response.Notes);
    }

    [Fact]
    public async Task List_HidesArchivedClientsUnlessAskedFor_AndClientsOfOtherWorkspaces()
    {
        var other = Workspace.Create("Other studio", Guid.NewGuid());
        _workspaces.Items.Add(other);
        var active = Client.Register(_workspace.Id, "Beta", "beta@example.com");
        var archived = Client.Register(_workspace.Id, "Alpha", "alpha@example.com");
        archived.Archive();
        _clients.Items.AddRange([active, archived, Client.Register(other.Id, "Theirs", "theirs@example.com")]);

        var defaultList = await _service.ListAsync(_workspace.Id, includeArchived: false, CancellationToken.None);
        var fullList = await _service.ListAsync(_workspace.Id, includeArchived: true, CancellationToken.None);

        Assert.Equal(active.Id, Assert.Single(defaultList).Id);
        Assert.Equal(["Alpha", "Beta"], fullList.Select(c => c.Name));
    }

    [Fact]
    public async Task Update_ChangesTheDetailsAndSavesOnce()
    {
        var client = Client.Register(_workspace.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(client);

        var response = await _service.UpdateAsync(
            _workspace.Id,
            client.Id,
            new UpdateClientRequest { Name = "Acme SRL", Email = "office@acme.com", Company = "Acme Group" },
            CancellationToken.None);

        Assert.Equal("Acme SRL", response.Name);
        Assert.Equal("office@acme.com", response.Email);
        Assert.Equal("Acme Group", response.Company);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_KeepingItsOwnEmail_IsNotAConflict()
    {
        var client = Client.Register(_workspace.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(client);

        var response = await _service.UpdateAsync(
            _workspace.Id,
            client.Id,
            new UpdateClientRequest { Name = "Acme SRL", Email = "CONTACT@acme.com" },
            CancellationToken.None);

        Assert.Equal("Acme SRL", response.Name);
    }

    [Fact]
    public async Task Update_ToTheEmailOfAnArchivedClient_ThrowsConflict_AndDoesNotSave()
    {
        var archived = Client.Register(_workspace.Id, "Old", "old@acme.com");
        archived.Archive();
        var client = Client.Register(_workspace.Id, "Acme", "contact@acme.com");
        _clients.Items.AddRange([archived, client]);

        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(
            _workspace.Id,
            client.Id,
            new UpdateClientRequest { Name = "Acme", Email = "old@acme.com" },
            CancellationToken.None));

        Assert.Equal("contact@acme.com", client.Email);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_ClientOfAnotherWorkspace_ThrowsNotFound()
    {
        var other = Workspace.Create("Other studio", Guid.NewGuid());
        _workspaces.Items.Add(other);
        var theirs = Client.Register(other.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(
            _workspace.Id,
            theirs.Id,
            new UpdateClientRequest { Name = "Hijacked", Email = "x@example.com" },
            CancellationToken.None));

        Assert.Equal("Acme", theirs.Name);
    }

    [Fact]
    public async Task ArchiveThenRestore_TogglesTheArchivedDate()
    {
        var client = Client.Register(_workspace.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(client);

        var archived = await _service.ArchiveAsync(_workspace.Id, client.Id, CancellationToken.None);
        var restored = await _service.RestoreAsync(_workspace.Id, client.Id, CancellationToken.None);

        Assert.NotNull(archived.ArchivedAtUtc);
        Assert.Null(restored.ArchivedAtUtc);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Archive_InAWorkspaceIAmNotAMemberOf_ThrowsNotFound()
    {
        var someoneElses = Workspace.Create("Not mine", Guid.NewGuid());
        _workspaces.Items.Add(someoneElses);
        var theirs = Client.Register(someoneElses.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ArchiveAsync(someoneElses.Id, theirs.Id, CancellationToken.None));

        Assert.False(theirs.IsArchived);
    }

    private sealed class FakeClientRepository : IClientRepository
    {
        public List<Client> Items { get; } = [];

        public Task<Client?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(c => c.WorkspaceId == workspaceId && c.Id == id));

        public Task<IReadOnlyList<Client>> ListAsync(Guid workspaceId, bool includeArchived, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Client>>(Items
                .Where(c => c.WorkspaceId == workspaceId && (includeArchived || !c.IsArchived))
                .OrderBy(c => c.Name)
                .ToList());

        public Task<bool> EmailExistsAsync(Guid workspaceId, string email, Guid? exceptClientId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(c => c.WorkspaceId == workspaceId && c.Email == email && c.Id != exceptClientId));

        public void Add(Client client) => Items.Add(client);
    }
}
