using Partnr.Application.Authorization;
using Partnr.Application.Exceptions;
using Partnr.Domain.Entities;

namespace Partnr.UnitTests.Application;

public class ProjectAccessTests
{
    private readonly FakeProjectRepository _projects = new();
    private readonly ProjectAccess _access;
    private readonly Guid _workspaceId = Guid.NewGuid();

    public ProjectAccessTests() => _access = new ProjectAccess(_projects);

    private Project AddProject(Guid workspaceId)
    {
        var project = Project.Create(workspaceId, Guid.NewGuid(), "Website");
        _projects.Items.Add(project);

        return project;
    }

    [Fact]
    public async Task RequireProject_FindsAProjectOfThisWorkspace_EvenWhenArchived()
    {
        var project = AddProject(_workspaceId);
        project.Archive();

        Assert.Same(project, await _access.RequireProjectAsync(_workspaceId, project.Id, CancellationToken.None));
    }

    [Fact]
    public async Task RequireProject_IsNotFound_ForAnotherWorkspacesProject_AndForAnUnknownId()
    {
        var elsewhere = AddProject(Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => _access.RequireProjectAsync(_workspaceId, elsewhere.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _access.RequireProjectAsync(_workspaceId, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task RequireEditableProject_RefusesAnArchivedProject_UntilItIsRestored()
    {
        var project = AddProject(_workspaceId);
        project.Archive();

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => _access.RequireEditableProjectAsync(_workspaceId, project.Id, CancellationToken.None));
        Assert.Equal("This project is archived. Restore it before making changes.", error.Message);

        project.Restore();
        Assert.Same(project, await _access.RequireEditableProjectAsync(_workspaceId, project.Id, CancellationToken.None));
    }

    [Fact]
    public async Task RequireEditableProject_IsNotFound_BeforeItIsConflict()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _access.RequireEditableProjectAsync(_workspaceId, Guid.NewGuid(), CancellationToken.None));
    }
}
