using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.DTOs.Deliverables;
using ClientPortal.Application.DTOs.Portal;
using ClientPortal.Application.DTOs.ProjectUpdates;
using ClientPortal.Application.DTOs.Projects;
using ClientPortal.Application.DTOs.Workspaces;

namespace ClientPortal.IntegrationTests.Portal;

[Collection(ApiCollection.Name)]
public sealed partial class PortalProjectsTests(ApiFactory factory)
{
    private const string Password = "correct horse";

    [Fact]
    public async Task Client_SeesAProjectOnceItLeavesPlanning_WithItsStagesInOrder()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var project = await studio.AddProject(elena, "Website", "Five pages");
        await studio.AddStage(project, "Design");
        await studio.AddStage(project, "Launch");

        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectSummaryResponse[]>("/api/portal/projects"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await elena.Http.GetAsync($"/api/portal/projects/{project.Id}")).StatusCode);

        await studio.SetStatus(project, "Active");

        var list = await elena.Http.GetFromJsonAsync<PortalProjectSummaryResponse[]>("/api/portal/projects");
        var shown = Assert.Single(list!);
        Assert.Equal((project.Id, "Website", "Active"), (shown.Id, shown.Name, shown.Status));
        Assert.Equal((studio.WorkspaceId, "Studio"), (shown.Workspace.Id, shown.Workspace.Name));
        Assert.Equal((0, 2), (shown.Progress.Done, shown.Progress.Total));

        var detail = await elena.Http.GetFromJsonAsync<PortalProjectResponse>($"/api/portal/projects/{project.Id}");
        Assert.Equal("Five pages", detail!.Description);
        Assert.Equal(["Design", "Launch"], detail.Stages.Select(s => s.Name));
    }

    [Fact]
    public async Task Project_GoingBackToPlanning_OrBeingArchived_DisappearsFromThePortal_AndComesBack()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");
        var url = $"/api/portal/projects/{project.Id}";
        Assert.Equal(HttpStatusCode.OK, (await elena.Http.GetAsync(url)).StatusCode);

        await studio.SetStatus(project, "Planning");
        Assert.Equal(HttpStatusCode.NotFound, (await elena.Http.GetAsync(url)).StatusCode);

        await studio.SetStatus(project, "OnHold");
        Assert.Equal(HttpStatusCode.OK, (await elena.Http.GetAsync(url)).StatusCode);

        await studio.Http.PostAsync($"/api/workspaces/{studio.WorkspaceId}/projects/{project.Id}/archive", null);
        Assert.Equal(HttpStatusCode.NotFound, (await elena.Http.GetAsync(url)).StatusCode);
        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectSummaryResponse[]>("/api/portal/projects"))!);

        await studio.Http.PostAsync($"/api/workspaces/{studio.WorkspaceId}/projects/{project.Id}/restore", null);
        Assert.Equal(HttpStatusCode.OK, (await elena.Http.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task ArchivingTheClient_TakesTheProjectsAway_AndRestoringGivesThemBack()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");
        var url = $"/api/portal/projects/{project.Id}";

        await studio.Http.PostAsync($"/api/workspaces/{studio.WorkspaceId}/clients/{elena.ClientId}/archive", null);
        Assert.Equal(HttpStatusCode.NotFound, (await elena.Http.GetAsync(url)).StatusCode);
        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectSummaryResponse[]>("/api/portal/projects"))!);

        await studio.Http.PostAsync($"/api/workspaces/{studio.WorkspaceId}/clients/{elena.ClientId}/restore", null);
        Assert.Equal(HttpStatusCode.OK, (await elena.Http.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Client_NeverSeesTheProjectsOfAnotherClient_EvenInTheSameWorkspace()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var bram = await studio.AddClient("Bram");
        var hers = await studio.AddProject(elena, "Elena's site");
        var his = await studio.AddProject(bram, "Bram's site");
        await studio.SetStatus(hers, "Active");
        await studio.SetStatus(his, "Active");

        var elenaList = await elena.Http.GetFromJsonAsync<PortalProjectSummaryResponse[]>("/api/portal/projects");
        var bramList = await bram.Http.GetFromJsonAsync<PortalProjectSummaryResponse[]>("/api/portal/projects");

        Assert.Equal(hers.Id, Assert.Single(elenaList!).Id);
        Assert.Equal(his.Id, Assert.Single(bramList!).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await elena.Http.GetAsync($"/api/portal/projects/{his.Id}")).StatusCode);
    }

    [Fact]
    public async Task PortalProject_ShowsNothingTheWorkspaceKeepsToItself()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena", notes: "Secret note: pays late");
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");
        await studio.AddStage(project, "Design");

        var detail = await elena.Http.GetStringAsync($"/api/portal/projects/{project.Id}");
        var list = await elena.Http.GetStringAsync("/api/portal/projects");

        foreach (var body in new[] { detail, list })
        {
            Assert.DoesNotContain("Secret note", body);
            Assert.DoesNotContain("clientId", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("notes", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task AMemberOfTheWorkspace_SeesNothingThroughThePortal_AndAnAnonymousVisitorGetsUnauthorized()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");

        // The workspace's own owner is not anyone's client: the portal is the client's view only.
        Assert.Empty((await studio.Http.GetFromJsonAsync<PortalProjectSummaryResponse[]>("/api/portal/projects"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await studio.Http.GetAsync($"/api/portal/projects/{project.Id}")).StatusCode);

        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/portal/projects")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/portal/projects/{project.Id}")).StatusCode);
    }

    [Fact]
    public async Task Progress_FollowsTheStagesTheWorkspaceFinishes()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");
        var design = (await studio.AddStage(project, "Design")).Stages[0];
        await studio.AddStage(project, "Launch");

        await studio.Http.PutAsJsonAsync(
            $"/api/workspaces/{studio.WorkspaceId}/projects/{project.Id}/stages/{design.Id}",
            new { name = "Design", status = "Done" });

        var detail = await elena.Http.GetFromJsonAsync<PortalProjectResponse>($"/api/portal/projects/{project.Id}");
        Assert.Equal((1, 2), (detail!.Progress.Done, detail.Progress.Total));
        Assert.Equal(["Done", "ToDo"], detail.Stages.Select(s => s.Status));
    }

    [Fact]
    public async Task Client_SeesADeliverableOnlyWhileItIsSharedAndNotArchived()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");
        var design = (await studio.AddStage(project, "Design")).Stages[0];
        var mockup = await studio.AddDeliverable(project, "Homepage mockup", design.Id);
        var url = $"/api/portal/projects/{project.Id}";

        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Deliverables);

        await studio.Http.PostAsync($"{studio.DeliverablesUrl(project)}/{mockup.Id}/share", null);
        var shared = Assert.Single((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Deliverables);
        Assert.Equal((mockup.Id, "Homepage mockup", "Design"), (shared.Id, shared.Title, shared.StageName));
        Assert.Equal("https://example.com/file", shared.Url);

        await studio.Http.PostAsync($"{studio.DeliverablesUrl(project)}/{mockup.Id}/unshare", null);
        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Deliverables);

        await studio.Http.PostAsync($"{studio.DeliverablesUrl(project)}/{mockup.Id}/share", null);
        await studio.Http.PostAsync($"{studio.DeliverablesUrl(project)}/{mockup.Id}/archive", null);
        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Deliverables);

        await studio.Http.PostAsync($"{studio.DeliverablesUrl(project)}/{mockup.Id}/restore", null);
        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Deliverables); // back as a draft
    }

    [Fact]
    public async Task SharedDeliverable_FollowsTheProject_AndNeverReachesAnotherClient()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var bram = await studio.AddClient("Bram");
        var hers = await studio.AddProject(elena, "Elena's site");
        var his = await studio.AddProject(bram, "Bram's site");
        await studio.SetStatus(hers, "Planning");
        await studio.SetStatus(his, "Active");
        var shared = await studio.AddDeliverable(hers, "Elena's mockup", null, share: true);

        // Shared, but the project is still a draft: there is nothing to see, and no way in through the project.
        Assert.Equal(HttpStatusCode.NotFound, (await elena.Http.GetAsync($"/api/portal/projects/{hers.Id}")).StatusCode);

        await studio.SetStatus(hers, "Active");
        Assert.Equal("Elena's mockup", Assert.Single((await elena.Http.GetFromJsonAsync<PortalProjectResponse>($"/api/portal/projects/{hers.Id}"))!.Deliverables).Title);

        var bramView = await bram.Http.GetStringAsync($"/api/portal/projects/{his.Id}");
        Assert.DoesNotContain(shared.Id.ToString(), bramView);
        Assert.DoesNotContain("Elena's mockup", bramView);
        Assert.Equal(HttpStatusCode.NotFound, (await bram.Http.GetAsync($"/api/portal/projects/{hers.Id}")).StatusCode);
    }

    [Fact]
    public async Task PortalDeliverable_ShowsNothingTheWorkspaceKeepsToItself()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");
        var design = (await studio.AddStage(project, "Design")).Stages[0];
        await studio.AddDeliverable(project, "Mockup", design.Id, share: true);

        var body = await elena.Http.GetStringAsync($"/api/portal/projects/{project.Id}");

        Assert.Contains("Mockup", body);
        foreach (var hidden in new[] { "workspaceId", "projectId", "stageId", "archivedAtUtc", "createdAtUtc" })
        {
            Assert.DoesNotContain($"\"{hidden}\"", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Client_SeesAnUpdateOnlyWhileItIsSharedAndNotArchived()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");
        var update = await studio.AddUpdate(project, "Design approved", "Starting the build.");
        var url = $"/api/portal/projects/{project.Id}";

        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Updates);

        await studio.Http.PostAsync($"{studio.UpdatesUrl(project)}/{update.Id}/share", null);
        var shared = Assert.Single((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Updates);
        Assert.Equal((update.Id, "Design approved", "Starting the build."), (shared.Id, shared.Title, shared.Body));

        await studio.Http.PostAsync($"{studio.UpdatesUrl(project)}/{update.Id}/unshare", null);
        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Updates);

        await studio.Http.PostAsync($"{studio.UpdatesUrl(project)}/{update.Id}/share", null);
        await studio.Http.PostAsync($"{studio.UpdatesUrl(project)}/{update.Id}/archive", null);
        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Updates);

        await studio.Http.PostAsync($"{studio.UpdatesUrl(project)}/{update.Id}/restore", null);
        Assert.Empty((await elena.Http.GetFromJsonAsync<PortalProjectResponse>(url))!.Updates); // back as a draft
    }

    [Fact]
    public async Task Updates_AreOrderedByWhenTheyWereShared_NotWhenTheyWereWritten()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");
        var writtenFirst = await studio.AddUpdate(project, "Written first", null);
        var writtenSecond = await studio.AddUpdate(project, "Written second", null);

        await studio.Http.PostAsync($"{studio.UpdatesUrl(project)}/{writtenSecond.Id}/share", null);
        await studio.Http.PostAsync($"{studio.UpdatesUrl(project)}/{writtenFirst.Id}/share", null);

        var updates = (await elena.Http.GetFromJsonAsync<PortalProjectResponse>($"/api/portal/projects/{project.Id}"))!.Updates;
        Assert.Equal(["Written first", "Written second"], updates.Select(u => u.Title));
    }

    [Fact]
    public async Task SharedUpdate_FollowsTheProject_AndNeverReachesAnotherClient()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var bram = await studio.AddClient("Bram");
        var hers = await studio.AddProject(elena, "Elena's site");
        var his = await studio.AddProject(bram, "Bram's site");
        await studio.SetStatus(hers, "Planning");
        await studio.SetStatus(his, "Active");
        var shared = await studio.AddUpdate(hers, "Elena's news", null, share: true);

        // Shared, but the project is still a draft: there is nothing to see, and no way in through the project.
        Assert.Equal(HttpStatusCode.NotFound, (await elena.Http.GetAsync($"/api/portal/projects/{hers.Id}")).StatusCode);

        await studio.SetStatus(hers, "Active");
        Assert.Equal("Elena's news", Assert.Single((await elena.Http.GetFromJsonAsync<PortalProjectResponse>($"/api/portal/projects/{hers.Id}"))!.Updates).Title);

        var bramView = await bram.Http.GetStringAsync($"/api/portal/projects/{his.Id}");
        Assert.DoesNotContain(shared.Id.ToString(), bramView);
        Assert.DoesNotContain("Elena's news", bramView);
        Assert.Equal(HttpStatusCode.NotFound, (await bram.Http.GetAsync($"/api/portal/projects/{hers.Id}")).StatusCode);
    }

    [Fact]
    public async Task PortalUpdate_ShowsNothingTheWorkspaceKeepsToItself()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");
        await studio.AddUpdate(project, "Design approved", "Starting the build.", share: true);

        using var body = JsonDocument.Parse(await elena.Http.GetStringAsync($"/api/portal/projects/{project.Id}"));

        var update = body.RootElement.GetProperty("updates")[0];
        Assert.Equal(["body", "id", "sharedAtUtc", "title"], update.EnumerateObject().Select(p => p.Name).Order());
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    [GeneratedRegex("/invite/([A-Za-z0-9_-]+)")]
    private static partial Regex InviteLink();

    private async Task<StudioSession> Studio()
    {
        var http = factory.CreateClient();
        var registered = await http.PostAsJsonAsync("/api/auth/register", new { email = NewEmail(), password = Password, fullName = "Owner" });
        registered.EnsureSuccessStatusCode();
        var workspace = await http.PostAsJsonAsync("/api/workspaces", new { name = "Studio" });
        Assert.Equal(HttpStatusCode.Created, workspace.StatusCode);

        return new StudioSession(factory, http, (await workspace.Content.ReadFromJsonAsync<WorkspaceResponse>())!.Id);
    }

    private sealed class StudioSession(ApiFactory factory, HttpClient http, Guid workspaceId)
    {
        public HttpClient Http { get; } = http;

        public Guid WorkspaceId { get; } = workspaceId;

        public async Task<ClientSession> AddClient(string name, string? notes = null)
        {
            var email = NewEmail();
            var added = await Http.PostAsJsonAsync($"/api/workspaces/{WorkspaceId}/clients", new { name, email, notes });
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
            var client = (await added.Content.ReadFromJsonAsync<ClientResponse>())!;

            var invited = await Http.PostAsync($"/api/workspaces/{WorkspaceId}/clients/{client.Id}/invitation", null);
            Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
            var token = InviteLink().Match(factory.Emails.SentTo(email)[^1].TextBody).Groups[1].Value;

            var clientHttp = factory.CreateClient();
            var accepted = await clientHttp.PostAsJsonAsync($"/api/invitations/{token}/register", new { fullName = name, password = Password });
            accepted.EnsureSuccessStatusCode();

            return new ClientSession(clientHttp, client.Id);
        }

        public async Task<ProjectResponse> AddProject(ClientSession client, string name, string? description = null)
        {
            var response = await Http.PostAsJsonAsync(
                $"/api/workspaces/{WorkspaceId}/projects",
                new { clientId = client.ClientId, name, description });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            return (await response.Content.ReadFromJsonAsync<ProjectResponse>())!;
        }

        public async Task<ProjectResponse> AddStage(ProjectResponse project, string name)
        {
            var response = await Http.PostAsJsonAsync($"/api/workspaces/{WorkspaceId}/projects/{project.Id}/stages", new { name });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            return (await response.Content.ReadFromJsonAsync<ProjectResponse>())!;
        }

        public string DeliverablesUrl(ProjectResponse project) => $"/api/workspaces/{WorkspaceId}/projects/{project.Id}/deliverables";

        public async Task<DeliverableResponse> AddDeliverable(ProjectResponse project, string title, Guid? stageId, bool share = false)
        {
            var response = await Http.PostAsJsonAsync(DeliverablesUrl(project), new { stageId, title, url = "https://example.com/file" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var deliverable = (await response.Content.ReadFromJsonAsync<DeliverableResponse>())!;
            if (share)
            {
                Assert.Equal(HttpStatusCode.OK, (await Http.PostAsync($"{DeliverablesUrl(project)}/{deliverable.Id}/share", null)).StatusCode);
            }

            return deliverable;
        }

        public string UpdatesUrl(ProjectResponse project) => $"/api/workspaces/{WorkspaceId}/projects/{project.Id}/updates";

        public async Task<ProjectUpdateResponse> AddUpdate(ProjectResponse project, string title, string? body, bool share = false)
        {
            var response = await Http.PostAsJsonAsync(UpdatesUrl(project), new { title, body });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var update = (await response.Content.ReadFromJsonAsync<ProjectUpdateResponse>())!;
            if (share)
            {
                Assert.Equal(HttpStatusCode.OK, (await Http.PostAsync($"{UpdatesUrl(project)}/{update.Id}/share", null)).StatusCode);
            }

            return update;
        }

        public async Task SetStatus(ProjectResponse project, string status)
        {
            var response = await Http.PutAsJsonAsync(
                $"/api/workspaces/{WorkspaceId}/projects/{project.Id}",
                new { name = project.Name, description = project.Description, status });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private sealed record ClientSession(HttpClient Http, Guid ClientId);
}
