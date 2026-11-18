using Partnr.Workspaces.Application.Authorization;
using Partnr.Workspaces.Application.Exceptions;

namespace Partnr.Workspaces.UnitTests.Application;

public class ProjectContentRulesTests
{
    [Theory]
    [InlineData("deliverable")]
    [InlineData("update")]
    [InlineData("file")]
    public void AnArchivedItem_IsConflict_AndNamesWhatItIs(string kind)
    {
        var error = Assert.Throws<ConflictException>(() => ProjectContentRules.EnsureNotArchived(isArchived: true, kind));

        Assert.Equal($"This {kind} is archived. Restore it before making changes.", error.Message);
    }

    [Fact]
    public void ALiveItem_PassesTheArchivedCheck() => ProjectContentRules.EnsureNotArchived(isArchived: false, "file");

    [Fact]
    public void OnlyADraftCanBeDeleted()
    {
        ProjectContentRules.EnsureDeletable(isDraft: true);

        Assert.Throws<ConflictException>(() => ProjectContentRules.EnsureDeletable(isDraft: false));
    }
}
