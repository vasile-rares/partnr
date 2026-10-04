using ClientPortal.Domain.Clients;
using ClientPortal.Domain.Common;

namespace ClientPortal.Tests.Domain;

public class ClientTests
{
    [Fact]
    public void Register_NormalizesInput()
    {
        var client = Client.Register("  Acme SRL ", "  Contact@Acme.COM ");

        Assert.Equal("Acme SRL", client.Name);
        Assert.Equal("contact@acme.com", client.Email);
        Assert.NotEqual(Guid.Empty, client.Id);
    }

    [Theory]
    [InlineData("", "contact@acme.com")]
    [InlineData("Acme", " ")]
    public void Register_WithMissingData_Throws(string name, string email) =>
        Assert.Throws<DomainException>(() => Client.Register(name, email));
}
