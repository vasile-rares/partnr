using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Events;

namespace ClientPortal.Domain.Tests.Entities;

public class ClientTests
{
    [Fact]
    public void Register_NormalizesInput_AndRaisesClientRegisteredEvent()
    {
        var client = Client.Register("  Acme SRL ", "  Contact@Acme.COM ");

        Assert.Equal("Acme SRL", client.Name);
        Assert.Equal("contact@acme.com", client.Email);
        var domainEvent = Assert.Single(client.DomainEvents);
        Assert.Equal(client.Id, Assert.IsType<ClientRegisteredEvent>(domainEvent).ClientId);
    }
}
