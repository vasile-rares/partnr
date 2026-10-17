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
public sealed class ProjectsTests(ApiFactory factory)
{
    [Fact]
    public async Task Create_StartsInPlanning_AndCanBeReadBack()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var acme = await AddClient(http, workspaceId, "Acme");

        var created = await Create(http, workspaceId, new
        {
            clientId = acme.Id,
            name = "Website",
            description = " ",
            startDate = "2026-10-01",
            dueDate = "2026-12-15",
        });
        var fetched = await http.GetFromJsonAsync<ProjectResponse>($"/api/workspaces/{workspaceId}/projects/{created.Id}");

        Assert.Equal("Planning", fetched!.Status);
        Assert.Equal(new ProjectClientResponse(acme.Id, "Acme"), fetched.Client);
        Assert.Null(fetched.Description);
        Assert.Equal(new DateOnly(2026, 12, 15), fetched.DueDate);
    }

    [Fact]
    public async Task Create_WithoutAClient_OrWithADueDateBeforeTheStart_ReturnsBadRequest()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var acme = await AddClient(http, workspaceId, "Acme");
        var url = $"/api/workspaces/{workspaceId}/projects";

        var noClient = await http.PostAsJsonAsync(url, new { name = "Website" });
        var badDates = await http.PostAsJsonAsync(url, new { clientId = acme.Id, name = "Website", startDate = "2026-12-01", dueDate = "2026-11-01" });

        Assert.Equal(HttpStatusCode.BadRequest, noClient.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badDates.StatusCode);
    }

    [Fact]
    public async Task Create_ForAnArchivedClient_ReturnsConflict()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var acme = await AddClient(http, workspaceId, "Acme");
        await http.PostAsync($"/api/workspaces/{workspaceId}/clients/{acme.Id}/archive", null);

        var response = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects", new { clientId = acme.Id, name = "Website" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_ForAClientOfAnotherWorkspace_ReturnsNotFound()
    {
        var (http, mine) = await SignedInWithWorkspace();
        var other = await CreateWorkspace(http, "Other");
        var theirs = await AddClient(http, other.Id, "Theirs");

        var response = await http.PostAsJsonAsync($"/api/workspaces/{mine}/projects", new { clientId = theirs.Id, name = "Website" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_FiltersByClient_AndHidesArchivedProjectsUnlessAskedFor()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var acme = await AddClient(http, workspaceId, "Acme");
        var bright = await AddClient(http, workspaceId, "Bright");
        var website = await Create(http, workspaceId, new { clientId = acme.Id, name = "Website", dueDate = "2026-12-01" });
        var old = await Create(http, workspaceId, new { clientId = acme.Id, name = "Old site" });
        var menu = await Create(http, workspaceId, new { clientId = bright.Id, name = "Menu", dueDate = "2026-11-01" });
        await http.PostAsync($"/api/workspaces/{workspaceId}/projects/{old.Id}/archive", null);
        var url = $"/api/workspaces/{workspaceId}/projects";

        var active = await http.GetFromJsonAsync<ProjectResponse[]>(url);
        var acmeAll = await http.GetFromJsonAsync<ProjectResponse[]>($"{url}?clientId={acme.Id}&includeArchived=true");

        Assert.Equal([menu.Id, website.Id], active!.Select(p => p.Id));
        Assert.Equal(["Old site", "Website"], acmeAll!.Select(p => p.Name).Order());
    }

    [Fact]
    public async Task Update_ChangesTheDetailsAndTheStatus_ButNotTheClient()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var acme = await AddClient(http, workspaceId, "Acme");
        var bright = await AddClient(http, workspaceId, "Bright");
        var project = await Create(http, workspaceId, new { clientId = acme.Id, name = "Website" });
        var url = $"/api/workspaces/{workspaceId}/projects/{project.Id}";

        // A clientId in the body is not part of the request, so it is ignored.
        var response = await http.PutAsJsonAsync(url, new { name = "New website", status = "Active", clientId = bright.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await http.GetFromJsonAsync<ProjectResponse>(url);
        Assert.Equal("New website", fetched!.Name);
        Assert.Equal("Active", fetched.Status);
        Assert.Equal(acme.Id, fetched.Client.Id);
    }

    [Fact]
    public async Task Update_WithAnUnknownStatus_ReturnsBadRequest()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var acme = await AddClient(http, workspaceId, "Acme");
        var project = await Create(http, workspaceId, new { clientId = acme.Id, name = "Website" });

        var response = await http.PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects/{project.Id}",
            new { name = "Website", status = "Done" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ArchivedProject_IsReadOnly_UntilRestored()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var acme = await AddClient(http, workspaceId, "Acme");
        var project = await Create(http, workspaceId, new { clientId = acme.Id, name = "Website" });
        var url = $"/api/workspaces/{workspaceId}/projects/{project.Id}";

        await http.PostAsync($"{url}/archive", null);
        var whileArchived = await http.PutAsJsonAsync(url, new { name = "Changed", status = "Active" });
        await http.PostAsync($"{url}/restore", null);
        var afterRestore = await http.PutAsJsonAsync(url, new { name = "Changed", status = "Active" });

        Assert.Equal(HttpStatusCode.Conflict, whileArchived.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterRestore.StatusCode);
    }

    [Fact]
    public async Task AnotherUser_GetsNotFound_OnListingReadingEditingAndArchiving()
    {
        var (owner, workspaceId) = await SignedInWithWorkspace();
        var acme = await AddClient(owner, workspaceId, "Acme");
        var project = await Create(owner, workspaceId, new { clientId = acme.Id, name = "Website" });
        var (stranger, _) = await SignedInWithWorkspace();
        var url = $"/api/workspaces/{workspaceId}/projects/{project.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/workspaces/{workspaceId}/projects")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync(url, new { name = "Hijacked", status = "Active" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"{url}/archive", null)).StatusCode);

        var unchanged = await owner.GetFromJsonAsync<ProjectResponse>(url);
        Assert.Equal("Website", unchanged!.Name);
        Assert.Null(unchanged.ArchivedAtUtc);
    }

    [Fact]
    public async Task Project_OfAnotherWorkspace_CannotBeReachedThroughMine()
    {
        var (http, mine) = await SignedInWithWorkspace();
        var other = await CreateWorkspace(http, "Other");
        var theirClient = await AddClient(http, other.Id, "Theirs");
        var theirs = await Create(http, other.Id, new { clientId = theirClient.Id, name = "Theirs" });

        var get = await http.GetAsync($"/api/workspaces/{mine}/projects/{theirs.Id}");
        var update = await http.PutAsJsonAsync($"/api/workspaces/{mine}/projects/{theirs.Id}", new { name = "Hijacked", status = "Active" });

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task Member_CanCreateAndEditProjects_ButNotArchiveOrRestoreThem()
    {
        var (owner, workspaceId) = await SignedInWithWorkspace();
        var acme = await AddClient(owner, workspaceId, "Acme");
        var project = await Create(owner, workspaceId, new { clientId = acme.Id, name = "Website" });
        var old = await Create(owner, workspaceId, new { clientId = acme.Id, name = "Old" });
        await owner.PostAsync($"/api/workspaces/{workspaceId}/projects/{old.Id}/archive", null);
        var (member, memberId) = await SignedIn();
        await AddMember(workspaceId, memberId);

        var created = await member.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects", new { clientId = acme.Id, name = "By member" });
        var edited = await member.PutAsJsonAsync($"/api/workspaces/{workspaceId}/projects/{project.Id}", new { name = "Website v2", status = "Active" });
        var archive = await member.PostAsync($"/api/workspaces/{workspaceId}/projects/{project.Id}/archive", null);
        var restore = await member.PostAsync($"/api/workspaces/{workspaceId}/projects/{old.Id}/restore", null);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, restore.StatusCode);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<(HttpClient Http, Guid WorkspaceId)> SignedInWithWorkspace()
    {
        var (http, _) = await SignedIn();
        var workspace = await CreateWorkspace(http, "Studio");

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

    private static async Task<WorkspaceResponse> CreateWorkspace(HttpClient http, string name)
    {
        var response = await http.PostAsJsonAsync("/api/workspaces", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkspaceResponse>())!;
    }

    private static async Task<ClientResponse> AddClient(HttpClient http, Guid workspaceId, string name)
    {
        var response = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/clients", new { name, email = NewEmail() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClientResponse>())!;
    }

    private static async Task<ProjectResponse> Create(HttpClient http, Guid workspaceId, object request)
    {
        var response = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>())!;
    }
}
