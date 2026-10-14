using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.UnitTests.Domain;

public class ProjectTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();

    [Fact]
    public void Create_StartsInPlanning_AndTrimsTheDetails()
    {
        var project = Project.Create(WorkspaceId, ClientId, "  Website  ", "  ", new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1));

        Assert.Equal(ProjectStatus.Planning, project.Status);
        Assert.Equal("Website", project.Name);
        Assert.Null(project.Description);
        Assert.Equal(ClientId, project.ClientId);
        Assert.False(project.IsArchived);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutAName_Throws(string name) =>
        Assert.Throws<DomainException>(() => Project.Create(WorkspaceId, ClientId, name));

    [Fact]
    public void Create_WithoutAWorkspaceOrClient_Throws()
    {
        Assert.Throws<DomainException>(() => Project.Create(Guid.Empty, ClientId, "Website"));
        Assert.Throws<DomainException>(() => Project.Create(WorkspaceId, Guid.Empty, "Website"));
    }

    [Fact]
    public void UpdateDetails_WithADueDateBeforeTheStart_Throws()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website");

        Assert.Throws<DomainException>(() =>
            project.UpdateDetails("Website", null, new DateOnly(2026, 12, 1), new DateOnly(2026, 11, 30)));
    }

    [Fact]
    public void UpdateDetails_WithTheSameStartAndDueDate_IsAllowed()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website");
        var day = new DateOnly(2026, 12, 1);

        project.UpdateDetails("Website", null, day, day);

        Assert.Equal(day, project.DueDate);
    }

    [Fact]
    public void ChangeStatus_CanGoBackToPlanning()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website");

        project.ChangeStatus(ProjectStatus.Completed);
        project.ChangeStatus(ProjectStatus.Planning);

        Assert.Equal(ProjectStatus.Planning, project.Status);
    }

    [Fact]
    public void Archive_Twice_KeepsTheFirstDate_AndRestoreClearsIt()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website");

        project.Archive();
        var first = project.ArchivedAtUtc;
        project.Archive();

        Assert.Equal(first, project.ArchivedAtUtc);

        project.Restore();
        Assert.False(project.IsArchived);
    }
}
