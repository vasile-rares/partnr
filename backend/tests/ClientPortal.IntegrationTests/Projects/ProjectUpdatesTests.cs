using System.Net;
using System.Net.Http.Json;
using ClientPortal.Application.DTOs.Auth;
using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.DTOs.ProjectUpdates;
using ClientPortal.Application.DTOs.Projects;
using ClientPortal.Application.DTOs.Workspaces;
using ClientPortal.Domain.Enums;
using ClientPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.IntegrationTests.Projects;

[Collection(ApiCollection.Name)]
public sealed class ProjectUpdatesTests(ApiFactory factory)
{
    [Fact]
    public async Task Create_StartsAsDraft_AndSurvivesBeingReadBack()
    {
        var (http, workspaceId, project) = await SignedInWithProject();

        var created = await Create(http, workspaceId, project.Id, new { title = "Design done", body = "Starting the build next week." });
        var fetched = await http.GetFromJsonAsync<ProjectUpdateResponse>($"{BaseUrl(workspaceId, project.Id)}/{created.Id}");

        Assert.Equal("Draft", fetched!.Status);
        Assert.Equal("Design done", fetched.Title);
        Assert.Equal("Starting the build next week.", fetched.Body);
        Assert.Null(fetched.SharedAtUtc);
    }

    [Fact]
    public async Task List_ShowsTheNewestFirst_AndHidesArchivedUnlessAskedFor()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var first = await Create(http, workspaceId, project.Id, Body("First"));
        var second = await Create(http, workspaceId, project.Id, Body("Second"));
        await http.PostAsync($"{BaseUrl(workspaceId, project.Id)}/{first.Id}/archive", null);

        var active = await http.GetFromJsonAsync<ProjectUpdateResponse[]>(BaseUrl(workspaceId, project.Id));
        var all = await http.GetFromJsonAsync<ProjectUpdateResponse[]>($"{BaseUrl(workspaceId, project.Id)}?includeArchived=true");

        Assert.Equal(second.Id, Assert.Single(active!).Id);
        Assert.Equal(["Second", "First"], all!.Select(u => u.Title));
    }

    [Fact]
    public async Task Create_WithoutATitle_ReturnsBadRequest()
    {
        var (http, workspaceId, project) = await SignedInWithProject();

        var response = await http.PostAsJsonAsync(BaseUrl(workspaceId, project.Id), new { title = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithATooLongBody_ReturnsBadRequest()
    {
        var (http, workspaceId, project) = await SignedInWithProject();

        var response = await http.PostAsJsonAsync(BaseUrl(workspaceId, project.Id), new { title = "Long", body = new string('x', 4001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesTheDetails_AndShareUnshareSwitchTheStatus_KeepingTheShareDate()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var created = await Create(http, workspaceId, project.Id, Body("Design done"));
        var url = $"{BaseUrl(workspaceId, project.Id)}/{created.Id}";

        var updated = await http.PutAsJsonAsync(url, new { title = "Design approved", body = "Next: build." });
        var shared = await http.PostAsync($"{url}/share", null);
        var unshared = await http.PostAsync($"{url}/unshare", null);

        var updatedBody = (await updated.Content.ReadFromJsonAsync<ProjectUpdateResponse>())!;
        var sharedBody = (await shared.Content.ReadFromJsonAsync<ProjectUpdateResponse>())!;
        var unsharedBody = (await unshared.Content.ReadFromJsonAsync<ProjectUpdateResponse>())!;
        Assert.Equal(("Design approved", "Next: build."), (updatedBody.Title, updatedBody.Body));
        Assert.Equal("Shared", sharedBody.Status);
        Assert.NotNull(sharedBody.SharedAtUtc);
        Assert.Equal("Draft", unsharedBody.Status);
        // Postgres keeps microseconds, .NET ticks are finer, so the value read back differs in the last digit.
        Assert.Equal(sharedBody.SharedAtUtc!.Value, unsharedBody.SharedAtUtc!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task UpdateOfAnotherProject_IsNotFound_AndStaysUntouched()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var other = await CreateProject(http, workspaceId, project.Client.Id, "Other");
        var theirs = await Create(http, workspaceId, other.Id, Body("Theirs"));
        var url = $"{BaseUrl(workspaceId, project.Id)}/{theirs.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.PutAsJsonAsync(url, Body("Hijacked"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.PostAsync($"{url}/share", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.DeleteAsync(url)).StatusCode);

        var untouched = await http.GetFromJsonAsync<ProjectUpdateResponse>($"{BaseUrl(workspaceId, other.Id)}/{theirs.Id}");
        Assert.Equal(("Theirs", "Draft"), (untouched!.Title, untouched.Status));
    }

    [Fact]
    public async Task Delete_WorksOnlyOnADraft()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var created = await Create(http, workspaceId, project.Id, Body("Design done"));
        var url = $"{BaseUrl(workspaceId, project.Id)}/{created.Id}";

        await http.PostAsync($"{url}/share", null);
        var whileShared = await http.DeleteAsync(url);
        await http.PostAsync($"{url}/unshare", null);
        var asDraft = await http.DeleteAsync(url);
        var afterwards = await http.GetAsync(url);

        Assert.Equal(HttpStatusCode.Conflict, whileShared.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, asDraft.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);
    }

    [Fact]
    public async Task Archive_ComesBackAsDraft_AndAnArchivedOneIsReadOnly()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var created = await Create(http, workspaceId, project.Id, Body("Design done"));
        var url = $"{BaseUrl(workspaceId, project.Id)}/{created.Id}";
        await http.PostAsync($"{url}/share", null);

        var archived = await http.PostAsync($"{url}/archive", null);
        var update = await http.PutAsJsonAsync(url, Body("Changed"));
        var share = await http.PostAsync($"{url}/share", null);
        var restored = await http.PostAsync($"{url}/restore", null);

        var archivedBody = (await archived.Content.ReadFromJsonAsync<ProjectUpdateResponse>())!;
        Assert.NotNull(archivedBody.ArchivedAtUtc);
        Assert.Equal("Draft", archivedBody.Status);
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, share.StatusCode);
        var restoredBody = (await restored.Content.ReadFromJsonAsync<ProjectUpdateResponse>())!;
        Assert.Null(restoredBody.ArchivedAtUtc);
        Assert.Equal("Draft", restoredBody.Status);
    }

    [Fact]
    public async Task ArchivedProject_UpdatesAreReadOnly_UntilRestored()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var created = await Create(http, workspaceId, project.Id, Body("Design done"));
        var url = $"{BaseUrl(workspaceId, project.Id)}/{created.Id}";
        var projectUrl = $"/api/workspaces/{workspaceId}/projects/{project.Id}";

        await http.PostAsync($"{projectUrl}/archive", null);
        var create = await http.PostAsJsonAsync(BaseUrl(workspaceId, project.Id), Body("New"));
        var update = await http.PutAsJsonAsync(url, Body("Changed"));
        var share = await http.PostAsync($"{url}/share", null);
        var delete = await http.DeleteAsync(url);
        var read = await http.GetAsync(url);
        await http.PostAsync($"{projectUrl}/restore", null);
        var afterRestore = await http.PostAsync($"{url}/share", null);

        Assert.All([create, update, share, delete], r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterRestore.StatusCode);
    }

    [Fact]
    public async Task Member_CanWork_ButOnlyTheOwnerCanArchiveAndRestore()
    {
        var (owner, workspaceId, project) = await SignedInWithProject();
        var (member, memberId) = await SignedIn();
        await AddMember(workspaceId, memberId);

        var created = await Create(member, workspaceId, project.Id, Body("By member"));
        var url = $"{BaseUrl(workspaceId, project.Id)}/{created.Id}";
        var share = await member.PostAsync($"{url}/share", null);
        var memberArchive = await member.PostAsync($"{url}/archive", null);
        await owner.PostAsync($"{url}/archive", null);
        var memberRestore = await member.PostAsync($"{url}/restore", null);

        Assert.Equal(HttpStatusCode.OK, share.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, memberArchive.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, memberRestore.StatusCode);
    }

    [Fact]
    public async Task AnotherUser_GetsNotFound_OnEverything()
    {
        var (owner, workspaceId, project) = await SignedInWithProject();
        var created = await Create(owner, workspaceId, project.Id, Body("Design done"));
        var (stranger, _) = await SignedInWithWorkspace();
        var url = $"{BaseUrl(workspaceId, project.Id)}/{created.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(BaseUrl(workspaceId, project.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync(BaseUrl(workspaceId, project.Id), Body("Hijacked"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync(url, Body("Hijacked"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"{url}/share", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync(url)).StatusCode);

        var untouched = await owner.GetFromJsonAsync<ProjectUpdateResponse>(url);
        Assert.Equal(("Design done", "Draft"), (untouched!.Title, untouched.Status));
    }

    [Fact]
    public async Task SignedOut_GetsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync(BaseUrl(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string BaseUrl(Guid workspaceId, Guid projectId) =>
        $"/api/workspaces/{workspaceId}/projects/{projectId}/updates";

    private static object Body(string title) => new { title };

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

    private static async Task<ProjectUpdateResponse> Create(HttpClient http, Guid workspaceId, Guid projectId, object request)
    {
        var response = await http.PostAsJsonAsync(BaseUrl(workspaceId, projectId), request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectUpdateResponse>())!;
    }
}
