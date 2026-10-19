using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClientPortal.Application.DTOs.Auth;
using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.DTOs.ProjectFiles;
using ClientPortal.Application.DTOs.Projects;
using ClientPortal.Application.DTOs.Workspaces;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using ClientPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.IntegrationTests.Projects;

[Collection(ApiCollection.Name)]
public sealed class ProjectFilesTests(ApiFactory factory)
{
    [Fact]
    public async Task Upload_StartsAsDraft_StoresTheBytes_AndSurvivesBeingReadBack()
    {
        var (http, workspaceId, project) = await SignedInWithProject();

        var created = await Upload(http, workspaceId, project.Id, "mockups.pdf", "pdf-bytes", name: "Homepage mockups", description: "Round 1");
        var fetched = await http.GetFromJsonAsync<ProjectFileResponse>($"{BaseUrl(workspaceId, project.Id)}/{created.Id}");

        Assert.Equal("Draft", fetched!.Status);
        Assert.Equal(("Homepage mockups", "Round 1"), (fetched.Name, fetched.Description));
        Assert.Equal(("mockups.pdf", "application/pdf", 9), (fetched.FileName, fetched.ContentType, fetched.SizeBytes));
        Assert.True(factory.Files.Contains($"{workspaceId}/{project.Id}/{created.Id}"));
    }

    [Fact]
    public async Task Upload_WithoutAName_UsesTheFileName()
    {
        var (http, workspaceId, project) = await SignedInWithProject();

        var created = await Upload(http, workspaceId, project.Id, "brand.fig", "figma");

        Assert.Equal("brand.fig", created.Name);
    }

    [Fact]
    public async Task Upload_OfADisallowedType_ReturnsBadRequest_AndStoresNothing()
    {
        var (http, workspaceId, project) = await SignedInWithProject();

        var response = await http.PostAsync(BaseUrl(workspaceId, project.Id), Form("setup.exe", "MZ"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Files.CountUnder($"{workspaceId}/{project.Id}/"));
    }

    [Fact]
    public async Task Upload_OfAnEmptyFile_ReturnsBadRequest()
    {
        var (http, workspaceId, project) = await SignedInWithProject();

        var response = await http.PostAsync(BaseUrl(workspaceId, project.Id), Form("empty.pdf", string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_OfAFileOverTheLimit_ReturnsBadRequest_AndAtTheLimitWorks()
    {
        var (http, workspaceId, project) = await SignedInWithProject();

        var over = await http.PostAsync(BaseUrl(workspaceId, project.Id), Form("big.zip", new byte[ProjectFile.MaxSizeBytes + 1]));
        var exactly = await http.PostAsync(BaseUrl(workspaceId, project.Id), Form("exact.zip", new byte[ProjectFile.MaxSizeBytes]));

        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Equal(HttpStatusCode.Created, exactly.StatusCode);
    }

    [Fact]
    public async Task Upload_WithoutAFile_ReturnsBadRequest()
    {
        var (http, workspaceId, project) = await SignedInWithProject();

        var response = await http.PostAsync(BaseUrl(workspaceId, project.Id), new MultipartFormDataContent { { new StringContent("Just a name"), "name" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WithATooLongName_ReturnsBadRequest()
    {
        var (http, workspaceId, project) = await SignedInWithProject();

        var response = await http.PostAsync(BaseUrl(workspaceId, project.Id), Form("a.pdf", "x", name: new string('n', 201)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ToAnArchivedProject_ReturnsConflict()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsync($"/api/workspaces/{workspaceId}/projects/{project.Id}/archive", null)).StatusCode);

        var response = await http.PostAsync(BaseUrl(workspaceId, project.Id), Form("a.pdf", "x"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Download_IsAlwaysAnAttachment_WithTheStoredTypeAndNoSniffing()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var created = await Upload(http, workspaceId, project.Id, "Logo ideas.svg", "<svg onload=\"alert(1)\"/>");

        var response = await http.GetAsync($"{BaseUrl(workspaceId, project.Id)}/{created.Id}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("<svg onload=\"alert(1)\"/>", await response.Content.ReadAsStringAsync());
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("Logo ideas.svg", response.Content.Headers.ContentDisposition.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName?.Trim('"'));
        Assert.Equal(24, response.Content.Headers.ContentLength);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Download_FromAnotherProject_IsNotFound()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var other = await CreateProject(http, workspaceId, project.Client.Id, "Other");
        var theirs = await Upload(http, workspaceId, other.Id, "theirs.pdf", "secret");

        var response = await http.GetAsync($"{BaseUrl(workspaceId, project.Id)}/{theirs.Id}/download");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_ShowsTheNewestFirst_AndHidesArchivedUnlessAskedFor()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var first = await Upload(http, workspaceId, project.Id, "first.pdf", "1");
        var second = await Upload(http, workspaceId, project.Id, "second.pdf", "2");
        await http.PostAsync($"{BaseUrl(workspaceId, project.Id)}/{first.Id}/archive", null);

        var active = await http.GetFromJsonAsync<ProjectFileResponse[]>(BaseUrl(workspaceId, project.Id));
        var all = await http.GetFromJsonAsync<ProjectFileResponse[]>($"{BaseUrl(workspaceId, project.Id)}?includeArchived=true");

        Assert.Equal(second.Id, Assert.Single(active!).Id);
        Assert.Equal(["second.pdf", "first.pdf"], all!.Select(f => f.FileName));
    }

    [Fact]
    public async Task Update_ChangesNameAndDescription_AndShareUnshareSwitchTheStatus()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var created = await Upload(http, workspaceId, project.Id, "mockups.pdf", "pdf");
        var url = $"{BaseUrl(workspaceId, project.Id)}/{created.Id}";

        var updated = await http.PutAsJsonAsync(url, new { name = "Final mockups", description = "Round 2" });
        var shared = await http.PostAsync($"{url}/share", null);
        var unshared = await http.PostAsync($"{url}/unshare", null);

        var updatedBody = (await updated.Content.ReadFromJsonAsync<ProjectFileResponse>())!;
        Assert.Equal(("Final mockups", "Round 2", "mockups.pdf"), (updatedBody.Name, updatedBody.Description, updatedBody.FileName));
        Assert.Equal("Shared", (await shared.Content.ReadFromJsonAsync<ProjectFileResponse>())!.Status);
        Assert.Equal("Draft", (await unshared.Content.ReadFromJsonAsync<ProjectFileResponse>())!.Status);
    }

    [Fact]
    public async Task Update_WithoutAName_ReturnsBadRequest()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var created = await Upload(http, workspaceId, project.Id, "mockups.pdf", "pdf");

        var response = await http.PutAsJsonAsync($"{BaseUrl(workspaceId, project.Id)}/{created.Id}", new { name = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ADraft_RemovesTheRowAndTheBytes()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var created = await Upload(http, workspaceId, project.Id, "mockups.pdf", "pdf");
        var url = $"{BaseUrl(workspaceId, project.Id)}/{created.Id}";

        var delete = await http.DeleteAsync(url);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(url)).StatusCode);
        Assert.False(factory.Files.Contains($"{workspaceId}/{project.Id}/{created.Id}"));
    }

    [Fact]
    public async Task Delete_ASharedFile_ReturnsConflict_AndKeepsTheBytes()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var created = await Upload(http, workspaceId, project.Id, "mockups.pdf", "pdf");
        var url = $"{BaseUrl(workspaceId, project.Id)}/{created.Id}";
        await http.PostAsync($"{url}/share", null);

        var delete = await http.DeleteAsync(url);

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.True(factory.Files.Contains($"{workspaceId}/{project.Id}/{created.Id}"));
    }

    [Fact]
    public async Task Archive_BringsASharedFileBackToDraft_AndAnArchivedFileIsReadOnly()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var created = await Upload(http, workspaceId, project.Id, "mockups.pdf", "pdf");
        var url = $"{BaseUrl(workspaceId, project.Id)}/{created.Id}";
        await http.PostAsync($"{url}/share", null);

        var archive = await http.PostAsync($"{url}/archive", null);
        var update = await http.PutAsJsonAsync(url, new { name = "Changed" });
        var share = await http.PostAsync($"{url}/share", null);
        var download = await http.GetAsync($"{url}/download");
        var restore = await http.PostAsync($"{url}/restore", null);

        var archived = (await archive.Content.ReadFromJsonAsync<ProjectFileResponse>())!;
        Assert.Equal("Draft", archived.Status);
        Assert.NotNull(archived.ArchivedAtUtc);
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, share.StatusCode);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        var restored = (await restore.Content.ReadFromJsonAsync<ProjectFileResponse>())!;
        Assert.Null(restored.ArchivedAtUtc);
        Assert.Equal("Draft", restored.Status);
    }

    [Fact]
    public async Task Member_CanUploadAndShare_ButNotArchive()
    {
        var (owner, workspaceId, project) = await SignedInWithProject();
        var (member, memberId) = await SignedIn();
        await AddMember(workspaceId, memberId);

        var created = await Upload(member, workspaceId, project.Id, "by-member.pdf", "pdf");
        var url = $"{BaseUrl(workspaceId, project.Id)}/{created.Id}";

        Assert.Equal(HttpStatusCode.OK, (await member.PostAsync($"{url}/share", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsync($"{url}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"{url}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsync($"{url}/restore", null)).StatusCode);
    }

    [Fact]
    public async Task AStrangerWithoutAccess_GetsNotFoundEverywhere()
    {
        var (owner, workspaceId, project) = await SignedInWithProject();
        var created = await Upload(owner, workspaceId, project.Id, "mockups.pdf", "pdf");
        var (stranger, _) = await SignedInWithWorkspace();
        var url = $"{BaseUrl(workspaceId, project.Id)}/{created.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(BaseUrl(workspaceId, project.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync(BaseUrl(workspaceId, project.Id), Form("hijack.pdf", "x"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{url}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync(url, new { name = "Hijacked" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"{url}/share", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync(url)).StatusCode);

        var untouched = await owner.GetFromJsonAsync<ProjectFileResponse>(url);
        Assert.Equal(("mockups.pdf", "Draft"), (untouched!.Name, untouched.Status));
    }

    [Fact]
    public async Task SignedOut_GetsUnauthorized()
    {
        var http = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync(BaseUrl(Guid.NewGuid(), Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsync(BaseUrl(Guid.NewGuid(), Guid.NewGuid()), Form("a.pdf", "x"))).StatusCode);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string BaseUrl(Guid workspaceId, Guid projectId) =>
        $"/api/workspaces/{workspaceId}/projects/{projectId}/files";

    private static MultipartFormDataContent Form(string fileName, string content, string? name = null, string? description = null) =>
        Form(fileName, Encoding.UTF8.GetBytes(content), name, description);

    private static MultipartFormDataContent Form(string fileName, byte[] content, string? name = null, string? description = null)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(content), "file", fileName } };
        if (name is not null)
        {
            form.Add(new StringContent(name), "name");
        }

        if (description is not null)
        {
            form.Add(new StringContent(description), "description");
        }

        return form;
    }

    private static async Task<ProjectFileResponse> Upload(
        HttpClient http,
        Guid workspaceId,
        Guid projectId,
        string fileName,
        string content,
        string? name = null,
        string? description = null)
    {
        var response = await http.PostAsync(BaseUrl(workspaceId, projectId), Form(fileName, content, name, description));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectFileResponse>())!;
    }

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
}
