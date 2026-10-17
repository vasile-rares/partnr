using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.UnitTests.Domain;

public class DeliverableTests
{
    private static Deliverable NewDeliverable(string url = "https://figma.com/file/abc") =>
        Deliverable.Create(Guid.NewGuid(), Guid.NewGuid(), null, "Homepage mockup", null, url);

    [Fact]
    public void Create_StartsAsDraft_AndTrimsTheText()
    {
        var deliverable = Deliverable.Create(Guid.NewGuid(), Guid.NewGuid(), null, "  Homepage  ", "  First pass  ", "https://figma.com/file/abc");

        Assert.Equal(DeliverableStatus.Draft, deliverable.Status);
        Assert.Equal("Homepage", deliverable.Title);
        Assert.Equal("First pass", deliverable.Description);
        Assert.False(deliverable.IsArchived);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutATitle_Throws(string title) =>
        Assert.Throws<DomainException>(() =>
            Deliverable.Create(Guid.NewGuid(), Guid.NewGuid(), null, title, null, "https://example.com"));

    [Theory]
    [InlineData("")]
    [InlineData("figma.com/file/abc")]
    [InlineData("http://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://example.com/file")]
    public void Create_WithALinkThatIsNotHttps_Throws(string url) =>
        Assert.Throws<DomainException>(() => NewDeliverable(url));

    [Fact]
    public void Create_WithoutAWorkspaceOrProject_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Deliverable.Create(Guid.Empty, Guid.NewGuid(), null, "Title", null, "https://example.com"));
        Assert.Throws<DomainException>(() =>
            Deliverable.Create(Guid.NewGuid(), Guid.Empty, null, "Title", null, "https://example.com"));
    }

    [Fact]
    public void UpdateDetails_ReplacesEverything_AndClearsTheOptionalFields()
    {
        var deliverable = Deliverable.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Old", "Notes", "https://example.com");

        deliverable.UpdateDetails(null, "New", " ", "https://example.com/v2");

        Assert.Equal("New", deliverable.Title);
        Assert.Null(deliverable.Description);
        Assert.Null(deliverable.StageId);
        Assert.Equal("https://example.com/v2", deliverable.Url);
    }

    [Fact]
    public void ShareAndUnshare_SwitchTheStatus()
    {
        var deliverable = NewDeliverable();

        deliverable.Share();
        Assert.Equal(DeliverableStatus.Shared, deliverable.Status);

        deliverable.Unshare();
        Assert.Equal(DeliverableStatus.Draft, deliverable.Status);
    }

    [Fact]
    public void Archive_GoesBackToDraft_SoRestoringNeverShowsItToTheClient()
    {
        var deliverable = NewDeliverable();
        deliverable.Share();

        deliverable.Archive();
        deliverable.Restore();

        Assert.False(deliverable.IsArchived);
        Assert.Equal(DeliverableStatus.Draft, deliverable.Status);
    }

    [Fact]
    public void Archive_Twice_KeepsTheFirstDate()
    {
        var deliverable = NewDeliverable();

        deliverable.Archive();
        var first = deliverable.ArchivedAtUtc;
        deliverable.Archive();

        Assert.Equal(first, deliverable.ArchivedAtUtc);
    }
}
