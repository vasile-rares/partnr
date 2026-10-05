using ClientPortal.Domain.Clients;
using ClientPortal.Domain.Common;

namespace ClientPortal.Tests.Domain;

public class ClientTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();

    [Fact]
    public void Register_NormalizesInput()
    {
        var client = Client.Register(WorkspaceId, "  Acme SRL ", "  Contact@Acme.COM ");

        Assert.Equal("Acme SRL", client.Name);
        Assert.Equal("contact@acme.com", client.Email);
        Assert.Equal(WorkspaceId, client.WorkspaceId);
        Assert.NotEqual(Guid.Empty, client.Id);
    }

    [Theory]
    [InlineData("", "contact@acme.com")]
    [InlineData("Acme", " ")]
    public void Register_WithMissingData_Throws(string name, string email) =>
        Assert.Throws<DomainException>(() => Client.Register(WorkspaceId, name, email));

    [Fact]
    public void Register_WithoutWorkspace_Throws() =>
        Assert.Throws<DomainException>(() => Client.Register(Guid.Empty, "Acme", "contact@acme.com"));
}
