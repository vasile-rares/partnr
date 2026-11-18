using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;

namespace Partnr.Workspaces.UnitTests.Domain;

/// <summary>The rule behind the portal: when may a client's account open a project?</summary>
public class ProjectClientAccessTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Client LinkedClient()
    {
        var client = Client.Register(WorkspaceId, "Elena", "elena@example.com");
        client.LinkTo(Guid.NewGuid(), Now);

        return client;
    }

    private static Project ProjectOf(Client client, ProjectStatus status)
    {
        var project = Project.Create(WorkspaceId, client.Id, "Website");
        project.ChangeStatus(status);

        return project;
    }

    [Theory]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.OnHold)]
    [InlineData(ProjectStatus.Completed)]
    public void IsOpen_OnceTheProjectHasLeftPlanning_ForAClientWithAnAccount(ProjectStatus status)
    {
        var client = LinkedClient();

        Assert.True(ProjectOf(client, status).IsOpenToClient(client));
    }

    [Fact]
    public void IsNotOpen_WhileThePlanIsStillBeingMade()
    {
        var client = LinkedClient();

        Assert.False(ProjectOf(client, ProjectStatus.Planning).IsOpenToClient(client));
    }

    [Fact]
    public void IsNotOpen_WhenTheProjectIsArchived()
    {
        var client = LinkedClient();
        var project = ProjectOf(client, ProjectStatus.Active);
        project.Archive();

        Assert.False(project.IsOpenToClient(client));
    }

    [Fact]
    public void IsNotOpen_WhenThereIsNoClientCard()
    {
        var client = LinkedClient();

        Assert.False(ProjectOf(client, ProjectStatus.Active).IsOpenToClient(null));
    }

    [Fact]
    public void IsNotOpen_WhenTheClientHasNoAccountYet()
    {
        var client = Client.Register(WorkspaceId, "Elena", "elena@example.com");

        Assert.False(ProjectOf(client, ProjectStatus.Active).IsOpenToClient(client));
    }

    [Fact]
    public void IsNotOpen_WhenTheClientCardIsArchived()
    {
        var client = LinkedClient();
        var project = ProjectOf(client, ProjectStatus.Active);
        client.Archive();

        Assert.False(project.IsOpenToClient(client));
    }

    [Fact]
    public void IsNotOpen_ForAnotherClientsCard()
    {
        var project = ProjectOf(LinkedClient(), ProjectStatus.Active);

        Assert.False(project.IsOpenToClient(LinkedClient()));
    }
}
