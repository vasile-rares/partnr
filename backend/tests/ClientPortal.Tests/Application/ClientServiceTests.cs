using ClientPortal.Application.Clients;
using ClientPortal.Application.Clients.Dtos.Requests;
using ClientPortal.Application.Common;
using ClientPortal.Application.Common.Exceptions;
using ClientPortal.Domain.Clients;

namespace ClientPortal.Tests.Application;

public class ClientServiceTests
{
    private readonly FakeClientRepository _clients = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly ClientService _service;

    public ClientServiceTests() => _service = new ClientService(_clients, _unitOfWork);

    [Fact]
    public async Task Register_NewEmail_AddsClientAndSavesOnce()
    {
        var response = await _service.RegisterAsync(
            new RegisterClientRequest { Name = "Acme", Email = "Contact@Acme.com" },
            CancellationToken.None);

        Assert.Equal("contact@acme.com", response.Email);
        Assert.Single(_clients.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Register_ExistingEmail_ThrowsConflict_AndDoesNotSave()
    {
        _clients.Items.Add(Client.Register("Acme", "contact@acme.com"));

        await Assert.ThrowsAsync<ConflictException>(() => _service.RegisterAsync(
            new RegisterClientRequest { Name = "Other", Email = "CONTACT@acme.com" },
            CancellationToken.None));

        Assert.Single(_clients.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetById_Existing_ReturnsClient()
    {
        var client = Client.Register("Acme", "contact@acme.com");
        _clients.Items.Add(client);

        var response = await _service.GetByIdAsync(client.Id, CancellationToken.None);

        Assert.Equal(client.Id, response.Id);
    }

    [Fact]
    public async Task GetById_Missing_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));

    private sealed class FakeClientRepository : IClientRepository
    {
        public List<Client> Items { get; } = [];

        public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(c => c.Id == id));

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(c => c.Email == email));

        public void Add(Client client) => Items.Add(client);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
