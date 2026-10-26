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

    [Fact]
    public void AddStage_AppendsToTheEnd_AsToDo()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website");

        var design = project.AddStage("  Design  ", " ", new DateOnly(2026, 11, 1));
        var launch = project.AddStage("Launch", null, null);

        Assert.Equal(["Design", "Launch"], project.Stages.Select(s => s.Name));
        Assert.Equal([0, 1], project.Stages.Select(s => s.Position));
        Assert.Equal(StageStatus.ToDo, design.Status);
        Assert.Null(design.Description);
        Assert.Equal(project.Id, launch.ProjectId);
    }

    [Fact]
    public void AddStage_WithoutAName_Throws()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website");

        Assert.Throws<DomainException>(() => project.AddStage(" ", null, null));
        Assert.Empty(project.Stages);
    }

    [Fact]
    public void UpdateStage_ChangesTheDetailsAndTheStatus_NotThePosition()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website");
        project.AddStage("Design", null, null);
        var launch = project.AddStage("Launch", null, null);

        project.UpdateStage(launch.Id, "Go live", "Publish it", new DateOnly(2026, 12, 15), StageStatus.InProgress);

        Assert.Equal("Go live", launch.Name);
        Assert.Equal(StageStatus.InProgress, launch.Status);
        Assert.Equal(1, launch.Position);
    }

    [Fact]
    public void AddStage_DueOutsideTheProjectsDates_Throws_ButTheDatesThemselvesAreFine()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website", null, new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 15));

        project.AddStage("First day", null, new DateOnly(2026, 10, 1));
        project.AddStage("Last day", null, new DateOnly(2026, 12, 15));
        var early = Assert.Throws<DomainException>(() => project.AddStage("Early", null, new DateOnly(2026, 9, 30)));
        var late = Assert.Throws<DomainException>(() => project.AddStage("Late", null, new DateOnly(2026, 12, 16)));

        Assert.Contains("before the project starts (1 Oct 2026)", early.Message);
        Assert.Contains("after the project is due (15 Dec 2026)", late.Message);
        Assert.Equal(["First day", "Last day"], project.Stages.Select(s => s.Name));
    }

    [Fact]
    public void Stages_OfAProjectWithOnlyOneDate_AreOnlyBoundedByThatDate()
    {
        var onlyDue = Project.Create(WorkspaceId, ClientId, "A", null, null, new DateOnly(2026, 12, 15));
        var onlyStart = Project.Create(WorkspaceId, ClientId, "B", null, new DateOnly(2026, 10, 1), null);

        onlyDue.AddStage("Long before", null, new DateOnly(2020, 1, 1));
        onlyStart.AddStage("Long after", null, new DateOnly(2030, 1, 1));

        Assert.Throws<DomainException>(() => onlyDue.AddStage("Late", null, new DateOnly(2026, 12, 16)));
        Assert.Throws<DomainException>(() => onlyStart.AddStage("Early", null, new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void UpdateStage_ToADateOutsideTheProject_Throws_AndChangesNothing()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website", null, new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 15));
        var design = project.AddStage("Design", "Layouts", new DateOnly(2026, 11, 1));

        Assert.Throws<DomainException>(() => project.UpdateStage(design.Id, "Renamed", null, new DateOnly(2027, 1, 1), StageStatus.Done));

        Assert.Equal(("Design", new DateOnly(2026, 11, 1), StageStatus.ToDo), (design.Name, design.DueDate, design.Status));
    }

    [Fact]
    public void UpdateDetails_ThatLeavesAStageOutside_Throws_ButClearingTheDatesIsFine()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website", null, new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 15));
        project.AddStage("Launch", null, new DateOnly(2026, 12, 15));

        var shorter = Assert.Throws<DomainException>(() => project.UpdateDetails("Website", null, new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1)));
        var later = Assert.Throws<DomainException>(() => project.UpdateDetails("Website", null, new DateOnly(2026, 12, 16), null));
        project.UpdateDetails("Website", null, null, null);

        Assert.Contains("\"Launch\" is due on 15 Dec 2026", shorter.Message);
        Assert.NotNull(later);
        Assert.Null(project.DueDate);
    }

    [Fact]
    public void UpdateStage_OfAnotherProject_Throws()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website");
        var other = Project.Create(WorkspaceId, ClientId, "Other");
        var foreign = other.AddStage("Design", null, null);

        Assert.Throws<DomainException>(() => project.UpdateStage(foreign.Id, "Hijacked", null, null, StageStatus.Done));
        Assert.Equal("Design", foreign.Name);
    }

    [Fact]
    public void MoveStage_SwapsWithTheNeighbour()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website");
        project.AddStage("A", null, null);
        var b = project.AddStage("B", null, null);
        project.AddStage("C", null, null);

        project.MoveStage(b.Id, up: true);
        Assert.Equal(["B", "A", "C"], project.Stages.Select(s => s.Name));

        project.MoveStage(b.Id, up: false);
        project.MoveStage(b.Id, up: false);
        Assert.Equal(["A", "C", "B"], project.Stages.Select(s => s.Name));
        Assert.Equal([0, 1, 2], project.Stages.Select(s => s.Position));
    }

    [Fact]
    public void MoveStage_AtEitherEnd_ChangesNothing()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website");
        var first = project.AddStage("A", null, null);
        var last = project.AddStage("B", null, null);

        project.MoveStage(first.Id, up: true);
        project.MoveStage(last.Id, up: false);

        Assert.Equal(["A", "B"], project.Stages.Select(s => s.Name));
    }

    [Fact]
    public void RemoveStage_ClosesTheGap()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website");
        project.AddStage("A", null, null);
        var b = project.AddStage("B", null, null);
        project.AddStage("C", null, null);

        project.RemoveStage(b.Id);

        Assert.Equal(["A", "C"], project.Stages.Select(s => s.Name));
        Assert.Equal([0, 1], project.Stages.Select(s => s.Position));
        Assert.False(project.HasStage(b.Id));
    }

    [Fact]
    public void DoneStageCount_CountsOnlyDoneStages()
    {
        var project = Project.Create(WorkspaceId, ClientId, "Website");
        var a = project.AddStage("A", null, null);
        var b = project.AddStage("B", null, null);
        project.AddStage("C", null, null);
        project.UpdateStage(a.Id, "A", null, null, StageStatus.Done);
        project.UpdateStage(b.Id, "B", null, null, StageStatus.InProgress);

        Assert.Equal(1, project.DoneStageCount);
        Assert.Equal(3, project.Stages.Count);
    }
}
