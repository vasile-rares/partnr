using System.Net;
using System.Net.Http.Json;
using ClientPortal.Application.DTOs.Auth;
using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.DTOs.Projects;
using ClientPortal.Application.DTOs.Workspaces;
using ClientPortal.Domain.Enums;
using ClientPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.IntegrationTests.Projects;

[Collection(ApiCollection.Name)]
public sealed class ProjectStagesTests(ApiFactory factory)
{
    [Fact]
    public async Task Stages_AreAddedInOrder_AndSurviveBeingReadBack()
    {
        var (http, workspaceId, project) = await SignedInWithProject();

        await AddStage(http, workspaceId, project.Id, new { name = "Design", description = "Layouts", dueDate = "2026-11-05" });
        await AddStage(http, workspaceId, project.Id, new { name = "Launch" });
        var fetched = await http.GetFromJsonAsync<ProjectResponse>($"/api/workspaces/{workspaceId}/projects/{project.Id}");

        Assert.Equal(["Design", "Launch"], fetched!.Stages.Select(s => s.Name));
        Assert.Equal("Layouts", fetched.Stages[0].Description);
        Assert.Equal(new DateOnly(2026, 11, 5), fetched.Stages[0].DueDate);
        Assert.All(fetched.Stages, s => Assert.Equal("ToDo", s.Status));
        Assert.Equal(new ProjectProgressResponse(0, 2), fetched.Progress);
    }

    [Fact]
    public async Task ProjectList_ShowsTheProgress()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var withStages = await AddStage(http, workspaceId, project.Id, new { name = "Design" });
        await AddStage(http, workspaceId, project.Id, new { name = "Launch" });
        await http.PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects/{project.Id}/stages/{withStages.Stages[0].Id}",
            new { name = "Design", status = "Done" });

        var list = await http.GetFromJsonAsync<ProjectResponse[]>($"/api/workspaces/{workspaceId}/projects");

        Assert.Equal(new ProjectProgressResponse(1, 2), Assert.Single(list!).Progress);
    }

    [Fact]
    public async Task UpdateStage_ChangesTheDetailsAndTheStatus()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var added = await AddStage(http, workspaceId, project.Id, new { name = "Design" });
        var url = $"/api/workspaces/{workspaceId}/projects/{project.Id}/stages/{added.Stages[0].Id}";

        var response = await http.PutAsJsonAsync(url, new { name = "UI design", description = "All pages", dueDate = "2026-11-05", status = "InProgress" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ProjectResponse>())!.Stages.Single();
        Assert.Equal("UI design", updated.Name);
        Assert.Equal("InProgress", updated.Status);
        Assert.Equal("All pages", updated.Description);
    }

    [Fact]
    public async Task UpdateStage_WithAnUnknownStatusOrNoName_ReturnsBadRequest()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var added = await AddStage(http, workspaceId, project.Id, new { name = "Design" });
        var url = $"/api/workspaces/{workspaceId}/projects/{project.Id}/stages/{added.Stages[0].Id}";

        var unknownStatus = await http.PutAsJsonAsync(url, new { name = "Design", status = "Finished" });
        var noName = await http.PutAsJsonAsync(url, new { name = " ", status = "Done" });
        var noNameOnCreate = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects/{project.Id}/stages", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, unknownStatus.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noNameOnCreate.StatusCode);
    }

    [Fact]
    public async Task MoveStage_ReordersThem_AndIsKeptAfterReloading()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        await AddStage(http, workspaceId, project.Id, new { name = "A" });
        await AddStage(http, workspaceId, project.Id, new { name = "B" });
        var three = await AddStage(http, workspaceId, project.Id, new { name = "C" });
        var c = three.Stages[2];

        var up = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects/{project.Id}/stages/{c.Id}/move", new { direction = "up" });
        var fetched = await http.GetFromJsonAsync<ProjectResponse>($"/api/workspaces/{workspaceId}/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.OK, up.StatusCode);
        Assert.Equal(["A", "C", "B"], fetched!.Stages.Select(s => s.Name));

        var first = fetched.Stages[0];
        var tooFar = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects/{project.Id}/stages/{first.Id}/move", new { direction = "up" });
        var unchanged = (await tooFar.Content.ReadFromJsonAsync<ProjectResponse>())!;
        Assert.Equal(HttpStatusCode.OK, tooFar.StatusCode);
        Assert.Equal(["A", "C", "B"], unchanged.Stages.Select(s => s.Name));
    }

    [Fact]
    public async Task MoveStage_WithAnUnknownDirection_ReturnsBadRequest()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var added = await AddStage(http, workspaceId, project.Id, new { name = "A" });

        var response = await http.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects/{project.Id}/stages/{added.Stages[0].Id}/move",
            new { direction = "sideways" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemoveStage_DeletesIt_AndTheOthersKeepTheirOrder()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        await AddStage(http, workspaceId, project.Id, new { name = "A" });
        var middle = (await AddStage(http, workspaceId, project.Id, new { name = "B" })).Stages[1];
        await AddStage(http, workspaceId, project.Id, new { name = "C" });

        var response = await http.DeleteAsync($"/api/workspaces/{workspaceId}/projects/{project.Id}/stages/{middle.Id}");
        var fetched = await http.GetFromJsonAsync<ProjectResponse>($"/api/workspaces/{workspaceId}/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["A", "C"], fetched!.Stages.Select(s => s.Name));
    }

    [Fact]
    public async Task StageOfAnotherProject_IsNotFound_AndStaysUntouched()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var other = await CreateProject(http, workspaceId, project.Client.Id, "Other");
        var foreign = (await AddStage(http, workspaceId, other.Id, new { name = "Design" })).Stages.Single();
        var url = $"/api/workspaces/{workspaceId}/projects/{project.Id}/stages/{foreign.Id}";

        var update = await http.PutAsJsonAsync(url, new { name = "Hijacked", status = "Done" });
        var move = await http.PostAsJsonAsync($"{url}/move", new { direction = "up" });
        var remove = await http.DeleteAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, move.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
        var untouched = await http.GetFromJsonAsync<ProjectResponse>($"/api/workspaces/{workspaceId}/projects/{other.Id}");
        Assert.Equal(("Design", "ToDo"), (untouched!.Stages.Single().Name, untouched.Stages.Single().Status));
    }

    [Fact]
    public async Task ArchivedProject_StagesAreReadOnly_UntilRestored()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var added = await AddStage(http, workspaceId, project.Id, new { name = "Design" });
        var stageUrl = $"/api/workspaces/{workspaceId}/projects/{project.Id}/stages/{added.Stages[0].Id}";
        var projectUrl = $"/api/workspaces/{workspaceId}/projects/{project.Id}";

        await http.PostAsync($"{projectUrl}/archive", null);
        var add = await http.PostAsJsonAsync($"{projectUrl}/stages", new { name = "New" });
        var update = await http.PutAsJsonAsync(stageUrl, new { name = "Changed", status = "Done" });
        var move = await http.PostAsJsonAsync($"{stageUrl}/move", new { direction = "down" });
        var remove = await http.DeleteAsync(stageUrl);
        await http.PostAsync($"{projectUrl}/restore", null);
        var afterRestore = await http.PutAsJsonAsync(stageUrl, new { name = "Changed", status = "Done" });

        Assert.All([add, update, move, remove], r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(HttpStatusCode.OK, afterRestore.StatusCode);
    }

    [Fact]
    public async Task AnotherUser_GetsNotFound_OnChangingStages()
    {
        var (owner, workspaceId, project) = await SignedInWithProject();
        var added = await AddStage(owner, workspaceId, project.Id, new { name = "Design" });
        var (stranger, _) = await SignedInWithWorkspace();
        var projectUrl = $"/api/workspaces/{workspaceId}/projects/{project.Id}";
        var stageUrl = $"{projectUrl}/stages/{added.Stages[0].Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"{projectUrl}/stages", new { name = "New" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync(stageUrl, new { name = "Hijacked", status = "Done" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync(stageUrl)).StatusCode);

        var unchanged = await owner.GetFromJsonAsync<ProjectResponse>(projectUrl);
        Assert.Equal("Design", Assert.Single(unchanged!.Stages).Name);
    }

    [Fact]
    public async Task Member_CanChangeStages()
    {
        var (owner, workspaceId, project) = await SignedInWithProject();
        var (member, memberId) = await SignedIn();
        await AddMember(workspaceId, memberId);

        var response = await member.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects/{project.Id}/stages", new { name = "By member" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await owner.GetFromJsonAsync<ProjectResponse>($"/api/workspaces/{workspaceId}/projects/{project.Id}");
        Assert.Equal("By member", Assert.Single(fetched!.Stages).Name);
    }

    [Fact]
    public async Task Stages_CannotBeDueOutsideTheProjectsDates_NorCanTheProjectMoveAwayFromThem()
    {
        var (http, workspaceId, _) = await SignedInWithProject();
        var clientResponse = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/clients", new { name = "Dated", email = NewEmail() });
        var client = (await clientResponse.Content.ReadFromJsonAsync<ClientResponse>())!;
        var projectUrl = $"/api/workspaces/{workspaceId}/projects";
        var created = await http.PostAsJsonAsync(projectUrl, new { clientId = client.Id, name = "Dated", startDate = "2026-10-01", dueDate = "2026-12-15" });
        var project = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!;
        var stagesUrl = $"{projectUrl}/{project.Id}/stages";

        var fits = await http.PostAsJsonAsync(stagesUrl, new { name = "Launch", dueDate = "2026-12-15" });
        var after = await http.PostAsJsonAsync(stagesUrl, new { name = "Late", dueDate = "2026-12-16" });
        var before = await http.PostAsJsonAsync(stagesUrl, new { name = "Early", dueDate = "2026-09-30" });
        var stage = (await fits.Content.ReadFromJsonAsync<ProjectResponse>())!.Stages.Single();
        var moved = await http.PutAsJsonAsync($"{stagesUrl}/{stage.Id}", new { name = "Launch", dueDate = "2027-01-01", status = "ToDo" });
        var shortened = await http.PutAsJsonAsync($"{projectUrl}/{project.Id}", new { name = "Dated", startDate = "2026-10-01", dueDate = "2026-12-01", status = "Planning" });

        Assert.Equal(HttpStatusCode.OK, fits.StatusCode);
        foreach (var refused in new[] { after, before, moved, shortened })
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }

        Assert.Contains("after the project is due", await after.Content.ReadAsStringAsync());
        Assert.Contains("Change the stage first", await shortened.Content.ReadAsStringAsync());
        var fetched = await http.GetFromJsonAsync<ProjectResponse>($"{projectUrl}/{project.Id}");
        Assert.Equal(["Launch"], fetched!.Stages.Select(s => s.Name));
        Assert.Equal(new DateOnly(2026, 12, 15), fetched.DueDate);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<(HttpClient Http, Guid WorkspaceId, ProjectResponse Project)> SignedInWithProject()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var clientResponse = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/clients", new { name = "Acme", email = NewEmail() });
        clientResponse.EnsureSuccessStatusCode();
        var client = (await clientResponse.Content.ReadFromJsonAsync<ClientResponse>())!;

        return (http, workspaceId, await CreateProject(http, workspaceId, client.Id, "Website"));
    }

    private async Task<(HttpClient Http, Guid WorkspaceId)> SignedInWithWorkspace()
    {
        var (http, _) = await SignedIn();
        var response = await http.PostAsJsonAsync("/api/workspaces", new { name = "Studio" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var workspace = (await response.Content.ReadFromJsonAsync<WorkspaceResponse>())!;

        return (http, workspace.Id);
    }

    private async Task<(HttpClient Http, Guid UserId)> SignedIn()
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync(
            "/api/auth/register",
            new { email = NewEmail(), password = "correct horse", fullName = "Test User" });
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();

        return (http, user!.Id);
    }

    private async Task AddMember(Guid workspaceId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var workspace = await db.Workspaces.FindAsync(workspaceId);
        await db.Entry(workspace!).Collection(w => w.Members).LoadAsync();
        workspace!.AddMember(userId, WorkspaceRole.Member);
        await db.SaveChangesAsync();
    }

    private static async Task<ProjectResponse> CreateProject(HttpClient http, Guid workspaceId, Guid clientId, string name)
    {
        var response = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects", new { clientId, name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>())!;
    }

    private static async Task<ProjectResponse> AddStage(HttpClient http, Guid workspaceId, Guid projectId, object request)
    {
        var response = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects/{projectId}/stages", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>())!;
    }
}
