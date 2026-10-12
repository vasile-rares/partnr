using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.UnitTests.Domain;

public class ClientTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();

    [Fact]
    public void Register_NormalizesInput()
    {
        var client = Client.Register(WorkspaceId, "  Acme SRL ", "  Contact@Acme.COM ", " Acme Group ", " ", null);

        Assert.Equal("Acme SRL", client.Name);
        Assert.Equal("contact@acme.com", client.Email);
        Assert.Equal("Acme Group", client.Company);
        Assert.Null(client.Phone);
        Assert.Null(client.Notes);
        Assert.Equal(WorkspaceId, client.WorkspaceId);
        Assert.NotEqual(Guid.Empty, client.Id);
        Assert.False(client.IsArchived);
    }

    [Theory]
    [InlineData("", "contact@acme.com")]
    [InlineData("Acme", " ")]
    public void Register_WithMissingData_Throws(string name, string email) =>
        Assert.Throws<DomainException>(() => Client.Register(WorkspaceId, name, email));

    [Fact]
    public void Register_WithoutWorkspace_Throws() =>
        Assert.Throws<DomainException>(() => Client.Register(Guid.Empty, "Acme", "contact@acme.com"));

    [Fact]
    public void UpdateDetails_ReplacesEverything_AndClearsBlankOptionalFields()
    {
        var client = Client.Register(WorkspaceId, "Acme", "contact@acme.com", "Acme Group", "0700", "VIP");

        client.UpdateDetails(" Acme SRL ", "NEW@acme.com", "", null, " Pays late ");

        Assert.Equal("Acme SRL", client.Name);
        Assert.Equal("new@acme.com", client.Email);
        Assert.Null(client.Company);
        Assert.Null(client.Phone);
        Assert.Equal("Pays late", client.Notes);
    }

    [Fact]
    public void UpdateDetails_WithoutName_Throws_AndChangesNothing()
    {
        var client = Client.Register(WorkspaceId, "Acme", "contact@acme.com");

        Assert.Throws<DomainException>(() => client.UpdateDetails(" ", "new@acme.com", null, null, null));
        Assert.Equal("contact@acme.com", client.Email);
    }

    [Fact]
    public void Archive_TwiceKeepsTheFirstDate_AndRestoreClearsIt()
    {
        var client = Client.Register(WorkspaceId, "Acme", "contact@acme.com");

        client.Archive();
        var archivedAt = client.ArchivedAtUtc;
        client.Archive();

        Assert.True(client.IsArchived);
        Assert.Equal(archivedAt, client.ArchivedAtUtc);

        client.Restore();

        Assert.False(client.IsArchived);
        Assert.Null(client.ArchivedAtUtc);
    }
}
