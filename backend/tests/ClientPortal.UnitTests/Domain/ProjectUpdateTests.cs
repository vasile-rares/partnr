using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.UnitTests.Domain;

public class ProjectUpdateTests
{
    private static ProjectUpdate NewUpdate() =>
        ProjectUpdate.Create(Guid.NewGuid(), Guid.NewGuid(), "Design done", null);

    [Fact]
    public void Create_StartsAsDraftWithoutAShareDate_AndTrimsTheText()
    {
        var update = ProjectUpdate.Create(Guid.NewGuid(), Guid.NewGuid(), "  Design done  ", "  Next up: build  ");

        Assert.Equal(ProjectUpdateStatus.Draft, update.Status);
        Assert.Equal("Design done", update.Title);
        Assert.Equal("Next up: build", update.Body);
        Assert.Null(update.SharedAtUtc);
        Assert.False(update.IsArchived);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutATitle_Throws(string title) =>
        Assert.Throws<DomainException>(() => ProjectUpdate.Create(Guid.NewGuid(), Guid.NewGuid(), title, null));

    [Fact]
    public void Create_WithoutAWorkspaceOrProject_Throws()
    {
        Assert.Throws<DomainException>(() => ProjectUpdate.Create(Guid.Empty, Guid.NewGuid(), "Title", null));
        Assert.Throws<DomainException>(() => ProjectUpdate.Create(Guid.NewGuid(), Guid.Empty, "Title", null));
    }

    [Fact]
    public void UpdateDetails_ReplacesEverything_AndClearsAnEmptyBody()
    {
        var update = ProjectUpdate.Create(Guid.NewGuid(), Guid.NewGuid(), "Old", "Notes");

        update.UpdateDetails("New", " ");

        Assert.Equal("New", update.Title);
        Assert.Null(update.Body);
    }

    [Fact]
    public void ShareAndUnshare_SwitchTheStatus()
    {
        var update = NewUpdate();

        update.Share();
        Assert.Equal(ProjectUpdateStatus.Shared, update.Status);

        update.Unshare();
        Assert.Equal(ProjectUpdateStatus.Draft, update.Status);
    }

    [Fact]
    public void Share_SetsTheShareDateOnce_SoSharingAgainKeepsTheOriginalPlaceInTheTimeline()
    {
        var update = NewUpdate();

        update.Share();
        var first = update.SharedAtUtc;
        update.Unshare();
        update.Share();

        Assert.NotNull(first);
        Assert.Equal(first, update.SharedAtUtc);
    }

    [Fact]
    public void Archive_GoesBackToDraft_SoRestoringNeverShowsItToTheClient()
    {
        var update = NewUpdate();
        update.Share();

        update.Archive();
        update.Restore();

        Assert.False(update.IsArchived);
        Assert.Equal(ProjectUpdateStatus.Draft, update.Status);
    }

    [Fact]
    public void Archive_Twice_KeepsTheFirstDate()
    {
        var update = NewUpdate();

        update.Archive();
        var first = update.ArchivedAtUtc;
        update.Archive();

        Assert.Equal(first, update.ArchivedAtUtc);
    }
}
