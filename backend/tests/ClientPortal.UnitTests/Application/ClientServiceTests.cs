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

    private sealed class FakeClientRepository : IClientRepository
    {
        public List<Client> Items { get; } = [];

        public Task<Client?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(c => c.WorkspaceId == workspaceId && c.Id == id));

        public Task<bool> EmailExistsAsync(Guid workspaceId, string email, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(c => c.WorkspaceId == workspaceId && c.Email == email));

        public void Add(Client client) => Items.Add(client);
    }
}
