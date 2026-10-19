using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.UnitTests.Domain;

public class ProjectFileTests
{
    private static ProjectFile NewFile(string fileName = "mockups.pdf", long size = 1024) =>
        ProjectFile.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, fileName, size);

    [Fact]
    public void Create_StartsAsDraft_NamedAfterTheFile_WithTheTypeFromTheExtension()
    {
        var file = NewFile("Homepage mockups.PDF");

        Assert.Equal(ProjectFileStatus.Draft, file.Status);
        Assert.Equal("Homepage mockups.PDF", file.Name);
        Assert.Equal("Homepage mockups.PDF", file.FileName);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(1024, file.SizeBytes);
        Assert.False(file.IsArchived);
    }

    [Fact]
    public void Create_WithANameAndDescription_TrimsThem()
    {
        var file = ProjectFile.Create(Guid.NewGuid(), Guid.NewGuid(), "  Mockups v2  ", "  For review  ", "m.pdf", 10);

        Assert.Equal("Mockups v2", file.Name);
        Assert.Equal("For review", file.Description);
    }

    [Theory]
    [InlineData("logo.fig")]
    [InlineData("hero.psd")]
    [InlineData("brand.AI")]
    [InlineData("app.sketch")]
    [InlineData("icons.svg")]
    [InlineData("site.zip")]
    [InlineData("font.woff2")]
    public void Create_AllowsDesignFilesAndOtherListedTypes(string fileName) =>
        Assert.NotNull(NewFile(fileName));

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("page.html")]
    [InlineData("run.js")]
    [InlineData("script.sh")]
    [InlineData("noextension")]
    [InlineData("archive.pdf.exe")]
    public void Create_RefusesOtherTypes(string fileName) =>
        Assert.Throws<DomainException>(() => NewFile(fileName));

    [Fact]
    public void Create_KeepsOnlyTheLastSegmentOfAPath_AndDropsControlCharacters()
    {
        var file = NewFile("C:\\Users\\me\\Desktop\\mock\tups.pdf");

        Assert.Equal("mockups.pdf", file.FileName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_RefusesAnEmptyFile(long size) =>
        Assert.Throws<DomainException>(() => NewFile(size: size));

    [Fact]
    public void Create_AcceptsExactlyTheLimit_AndRefusesOneByteMore()
    {
        Assert.NotNull(NewFile(size: ProjectFile.MaxSizeBytes));
        Assert.Throws<DomainException>(() => NewFile(size: ProjectFile.MaxSizeBytes + 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dir/")]
    public void Create_WithoutAFileName_Throws(string fileName) =>
        Assert.Throws<DomainException>(() => NewFile(fileName));

    [Fact]
    public void Create_WithATooLongFileName_Throws() =>
        Assert.Throws<DomainException>(() => NewFile(new string('a', ProjectFile.FileNameMaxLength) + ".pdf"));

    [Fact]
    public void Create_WithoutAWorkspaceOrProject_Throws()
    {
        Assert.Throws<DomainException>(() => ProjectFile.Create(Guid.Empty, Guid.NewGuid(), null, null, "a.pdf", 1));
        Assert.Throws<DomainException>(() => ProjectFile.Create(Guid.NewGuid(), Guid.Empty, null, null, "a.pdf", 1));
    }

    [Fact]
    public void StorageKey_IsBuiltFromIdsOnly()
    {
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var file = ProjectFile.Create(workspaceId, projectId, null, null, "secret name.pdf", 1);

        Assert.Equal($"{workspaceId}/{projectId}/{file.Id}", file.StorageKey);
    }

    [Fact]
    public void UpdateDetails_ReplacesTheNameAndDescription_ButNotTheFile()
    {
        var file = ProjectFile.Create(Guid.NewGuid(), Guid.NewGuid(), null, "Notes", "m.pdf", 10);

        file.UpdateDetails("Final mockups", " ");

        Assert.Equal("Final mockups", file.Name);
        Assert.Null(file.Description);
        Assert.Equal("m.pdf", file.FileName);
    }

    [Fact]
    public void UpdateDetails_WithoutAName_Throws() =>
        Assert.Throws<DomainException>(() => NewFile().UpdateDetails(" ", null));

    [Fact]
    public void ShareThenUnshare_SwitchesTheStatus()
    {
        var file = NewFile();

        file.Share();
        Assert.Equal(ProjectFileStatus.Shared, file.Status);

        file.Unshare();
        Assert.Equal(ProjectFileStatus.Draft, file.Status);
    }

    [Fact]
    public void Archive_ReturnsToDraft_AndRestoreDoesNotShareAgain()
    {
        var file = NewFile();
        file.Share();

        file.Archive();
        Assert.True(file.IsArchived);
        Assert.Equal(ProjectFileStatus.Draft, file.Status);

        file.Restore();
        Assert.False(file.IsArchived);
        Assert.Equal(ProjectFileStatus.Draft, file.Status);
    }
}
