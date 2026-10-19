using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClientPortal.Application.DTOs.Portal;
using ClientPortal.Application.DTOs.ProjectFiles;
using ClientPortal.Application.DTOs.Projects;

namespace ClientPortal.IntegrationTests.Portal;

public sealed partial class PortalProjectsTests
{
    [Fact]
    public async Task Client_SeesAFileOnlyWhileItIsSharedAndNotArchived()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");
        var file = await AddFile(studio, project, "mockups.pdf", "pdf-bytes", name: "Homepage mockups", description: "Round 1");
        var url = $"/api/portal/projects/{project.Id}";
        var filesUrl = $"/api/workspaces/{studio.WorkspaceId}/projects/{project.Id}/files/{file.Id}";

        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Files);

        await studio.Http.PostAsync($"{filesUrl}/share", null);
        var shared = Assert.Single((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Files);
        Assert.Equal((file.Id, "Homepage mockups", "Round 1", "mockups.pdf", 9), (shared.Id, shared.Name, shared.Description, shared.FileName, shared.SizeBytes));

        await studio.Http.PostAsync($"{filesUrl}/unshare", null);
        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Files);

        await studio.Http.PostAsync($"{filesUrl}/share", null);
        await studio.Http.PostAsync($"{filesUrl}/archive", null);
        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Files);

        await studio.Http.PostAsync($"{filesUrl}/restore", null);
        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Files); // back as a draft
    }

    [Fact]
    public async Task Client_DownloadsASharedFile_AsAnAttachmentThatTheBrowserMustNotSniff()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");
        var file = await AddFile(studio, project, "Logo ideas.svg", "<svg onload=\"alert(1)\"/>", share: true);

        var response = await elena.Http.GetAsync($"/api/portal/projects/{project.Id}/files/{file.Id}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("<svg onload=\"alert(1)\"/>", await response.Content.ReadAsStringAsync());
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Client_CannotDownloadAFileThatIsNotSharedWithThem()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var bram = await studio.AddClient("Bram");
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");
        var draft = await AddFile(studio, project, "draft.pdf", "draft");
        var unshared = await AddFile(studio, project, "unshared.pdf", "unshared", share: true);
        var archived = await AddFile(studio, project, "archived.pdf", "archived", share: true);
        var shared = await AddFile(studio, project, "shared.pdf", "shared", share: true);
        var filesUrl = $"/api/workspaces/{studio.WorkspaceId}/projects/{project.Id}/files";
        await studio.Http.PostAsync($"{filesUrl}/{unshared.Id}/unshare", null);
        await studio.Http.PostAsync($"{filesUrl}/{archived.Id}/archive", null);
        string Download(Guid id) => $"/api/portal/projects/{project.Id}/files/{id}/download";

        foreach (var hidden in new[] { draft.Id, unshared.Id, archived.Id, Guid.NewGuid() })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await elena.Http.GetAsync(Download(hidden))).StatusCode);
        }

        // Someone else's project is as invisible as a missing one, even with a file that is shared.
        Assert.Equal(HttpStatusCode.OK, (await elena.Http.GetAsync(Download(shared.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bram.Http.GetAsync(Download(shared.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(Download(shared.Id))).StatusCode);
    }

    [Fact]
    public async Task Client_CannotDownloadFromAProjectInPlanning()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var project = await studio.AddProject(elena, "Not yet");
        var file = await AddFile(studio, project, "early.pdf", "early", share: true);

        var response = await elena.Http.GetAsync($"/api/portal/projects/{project.Id}/files/{file.Id}/download");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PortalFile_ShowsNothingTheWorkspaceKeepsToItself()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");
        await AddFile(studio, project, "mockups.pdf", "pdf", share: true);

        using var body = JsonDocument.Parse(await elena.Http.GetStringAsync($"/api/portal/projects/{project.Id}"));

        var file = body.RootElement.GetProperty("files")[0];
        Assert.Equal(["description", "fileName", "id", "name", "sizeBytes"], file.EnumerateObject().Select(p => p.Name).Order());
    }

    private static async Task<ProjectFileResponse> AddFile(
        StudioSession studio,
        ProjectResponse project,
        string fileName,
        string content,
        bool share = false,
        string? name = null,
        string? description = null)
    {
        var filesUrl = $"/api/workspaces/{studio.WorkspaceId}/projects/{project.Id}/files";
        var form = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", fileName } };
        if (name is not null)
        {
            form.Add(new StringContent(name), "name");
        }

        if (description is not null)
        {
            form.Add(new StringContent(description), "description");
        }

        var response = await studio.Http.PostAsync(filesUrl, form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var file = (await response.Content.ReadFromJsonAsync<ProjectFileResponse>())!;
        if (share)
        {
            Assert.Equal(HttpStatusCode.OK, (await studio.Http.PostAsync($"{filesUrl}/{file.Id}/share", null)).StatusCode);
        }

        return file;
    }
}
