using System.Net;
using System.Net.Http.Json;
using ClientPortal.Application.DTOs.Auth;
using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.DTOs.Deliverables;
using ClientPortal.Application.DTOs.Projects;
using ClientPortal.Application.DTOs.Workspaces;
using ClientPortal.Domain.Enums;
using ClientPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.IntegrationTests.Projects;

[Collection(ApiCollection.Name)]
public sealed class ApprovalsTests(ApiFactory factory)
{
    [Fact]
    public async Task Request_OpensAPendingRound_AndTheDeliverableReadsBackWithIt()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var deliverable = await SharedDeliverable(http, workspaceId, project.Id, "Homepage");

        var response = await http.PostAsJsonAsync(ApprovalsUrl(workspaceId, project.Id, deliverable.Id), new { note = "Check the mobile layout." });
        var fetched = await http.GetFromJsonAsync<DeliverableResponse>($"{DeliverablesUrl(workspaceId, project.Id)}/{deliverable.Id}");
        var listed = (await http.GetFromJsonAsync<DeliverableResponse[]>(DeliverablesUrl(workspaceId, project.Id)))!.Single();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Pending", fetched!.ApprovalStatus);
        var round = Assert.Single(fetched.Approvals);
        Assert.Equal(("Pending", "Check the mobile layout."), (round.Status, round.Note));
        Assert.Null(round.ResolvedAtUtc);
        Assert.Equal("Pending", listed.ApprovalStatus);
    }

    [Fact]
    public async Task Request_WithoutANote_IsAllowed_AndATooLongNoteIsRefused()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var deliverable = await SharedDeliverable(http, workspaceId, project.Id, "Homepage");
        var url = ApprovalsUrl(workspaceId, project.Id, deliverable.Id);

        var tooLong = await http.PostAsJsonAsync(url, new { note = new string('x', 2001) });
        var withoutNote = await http.PostAsJsonAsync(url, new { });

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withoutNote.StatusCode);
        Assert.Null((await withoutNote.Content.ReadFromJsonAsync<DeliverableResponse>())!.Approvals.Single().Note);
    }

    [Fact]
    public async Task Request_OnADraft_ReturnsConflict()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var draft = await CreateDeliverable(http, workspaceId, project.Id, "Draft");

        var response = await http.PostAsJsonAsync(ApprovalsUrl(workspaceId, project.Id, draft.Id), new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task OnlyOneRoundCanBeOpen_AndANewOneComesAfterTheOldOneClosed()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var deliverable = await SharedDeliverable(http, workspaceId, project.Id, "Homepage");
        var url = ApprovalsUrl(workspaceId, project.Id, deliverable.Id);
        var first = (await (await http.PostAsJsonAsync(url, new { })).Content.ReadFromJsonAsync<DeliverableResponse>())!.Approvals.Single();

        var second = await http.PostAsJsonAsync(url, new { });
        await Decide(first.Id, approve: true);
        var third = await http.PostAsJsonAsync(url, new { });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        var body = (await third.Content.ReadFromJsonAsync<DeliverableResponse>())!;
        Assert.Equal(["Pending", "Approved"], body.Approvals.Select(a => a.Status));
        Assert.Equal("Pending", body.ApprovalStatus);
    }

    [Fact]
    public async Task Withdraw_ClosesTheRound_AndAClosedRoundCannotBeWithdrawnAgain()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var deliverable = await SharedDeliverable(http, workspaceId, project.Id, "Homepage");
        var url = ApprovalsUrl(workspaceId, project.Id, deliverable.Id);
        var round = (await (await http.PostAsJsonAsync(url, new { })).Content.ReadFromJsonAsync<DeliverableResponse>())!.Approvals.Single();

        var withdrawn = await http.PostAsync($"{url}/{round.Id}/withdraw", null);
        var again = await http.PostAsync($"{url}/{round.Id}/withdraw", null);

        var body = (await withdrawn.Content.ReadFromJsonAsync<DeliverableResponse>())!;
        Assert.Equal("None", body.ApprovalStatus);
        Assert.Equal("Withdrawn", body.Approvals.Single().Status);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Edit_IsBlockedWhileARequestIsOpen_AndFreeAfterTheClientDecided()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var deliverable = await SharedDeliverable(http, workspaceId, project.Id, "Homepage");
        var deliverableUrl = $"{DeliverablesUrl(workspaceId, project.Id)}/{deliverable.Id}";
        var round = (await (await http.PostAsJsonAsync(ApprovalsUrl(workspaceId, project.Id, deliverable.Id), new { })).Content
            .ReadFromJsonAsync<DeliverableResponse>())!.Approvals.Single();

        var blocked = await http.PutAsJsonAsync(deliverableUrl, new { title = "Changed", url = "https://example.com/v2" });
        await Decide(round.Id, approve: false, message: "Bigger logo.");
        var allowed = await http.PutAsJsonAsync(deliverableUrl, new { title = "Changed", url = "https://example.com/v2" });

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var body = (await allowed.Content.ReadFromJsonAsync<DeliverableResponse>())!;
        Assert.Equal(("Changed", "ChangesRequested", "Bigger logo."), (body.Title, body.ApprovalStatus, body.Approvals.Single().Message));
    }

    [Fact]
    public async Task Unshare_AndArchive_WithdrawAnOpenRequest()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var first = await SharedDeliverable(http, workspaceId, project.Id, "First");
        var second = await SharedDeliverable(http, workspaceId, project.Id, "Second");
        await http.PostAsJsonAsync(ApprovalsUrl(workspaceId, project.Id, first.Id), new { });
        await http.PostAsJsonAsync(ApprovalsUrl(workspaceId, project.Id, second.Id), new { });

        var unshared = await http.PostAsync($"{DeliverablesUrl(workspaceId, project.Id)}/{first.Id}/unshare", null);
        var archived = await http.PostAsync($"{DeliverablesUrl(workspaceId, project.Id)}/{second.Id}/archive", null);

        foreach (var response in new[] { unshared, archived })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = (await response.Content.ReadFromJsonAsync<DeliverableResponse>())!;
            Assert.Equal("None", body.ApprovalStatus);
            Assert.Equal("Withdrawn", body.Approvals.Single().Status);
        }
    }

    [Fact]
    public async Task Delete_IsRefusedForADeliverableWithAnApprovalHistory_ButNotForAPlainDraft()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var withHistory = await SharedDeliverable(http, workspaceId, project.Id, "With history");
        var plain = await CreateDeliverable(http, workspaceId, project.Id, "Plain");
        await http.PostAsJsonAsync(ApprovalsUrl(workspaceId, project.Id, withHistory.Id), new { });
        await http.PostAsync($"{DeliverablesUrl(workspaceId, project.Id)}/{withHistory.Id}/unshare", null); // back to Draft, history stays

        var refused = await http.DeleteAsync($"{DeliverablesUrl(workspaceId, project.Id)}/{withHistory.Id}");
        var allowed = await http.DeleteAsync($"{DeliverablesUrl(workspaceId, project.Id)}/{plain.Id}");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        var kept = await http.GetFromJsonAsync<DeliverableResponse>($"{DeliverablesUrl(workspaceId, project.Id)}/{withHistory.Id}");
        Assert.Single(kept!.Approvals);
    }

    [Fact]
    public async Task ArchivedProject_RefusesNewAndWithdrawnRequests_ButStillShowsTheHistory()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var deliverable = await SharedDeliverable(http, workspaceId, project.Id, "Homepage");
        var url = ApprovalsUrl(workspaceId, project.Id, deliverable.Id);
        var round = (await (await http.PostAsJsonAsync(url, new { })).Content.ReadFromJsonAsync<DeliverableResponse>())!.Approvals.Single();

        await http.PostAsync($"/api/workspaces/{workspaceId}/projects/{project.Id}/archive", null);
        var request = await http.PostAsJsonAsync(url, new { });
        var withdraw = await http.PostAsync($"{url}/{round.Id}/withdraw", null);
        var read = await http.GetFromJsonAsync<DeliverableResponse>($"{DeliverablesUrl(workspaceId, project.Id)}/{deliverable.Id}");

        Assert.Equal(HttpStatusCode.Conflict, request.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, withdraw.StatusCode);
        Assert.Equal("Pending", read!.ApprovalStatus);
    }

    [Fact]
    public async Task Member_CanRequestAndWithdraw()
    {
        var (owner, workspaceId, project) = await SignedInWithProject();
        var (member, memberId) = await SignedIn();
        await AddMember(workspaceId, memberId);
        var deliverable = await SharedDeliverable(owner, workspaceId, project.Id, "Homepage");
        var url = ApprovalsUrl(workspaceId, project.Id, deliverable.Id);

        var requested = await member.PostAsJsonAsync(url, new { note = "From a member." });
        var round = (await requested.Content.ReadFromJsonAsync<DeliverableResponse>())!.Approvals.Single();
        var withdrawn = await member.PostAsync($"{url}/{round.Id}/withdraw", null);

        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
    }

    [Fact]
    public async Task AnotherUser_GetsNotFound_AndTheRoundStaysOpen()
    {
        var (owner, workspaceId, project) = await SignedInWithProject();
        var deliverable = await SharedDeliverable(owner, workspaceId, project.Id, "Homepage");
        var url = ApprovalsUrl(workspaceId, project.Id, deliverable.Id);
        var round = (await (await owner.PostAsJsonAsync(url, new { })).Content.ReadFromJsonAsync<DeliverableResponse>())!.Approvals.Single();
        var (stranger, _) = await SignedInWithWorkspace();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync(url, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"{url}/{round.Id}/withdraw", null)).StatusCode);

        var untouched = await owner.GetFromJsonAsync<DeliverableResponse>($"{DeliverablesUrl(workspaceId, project.Id)}/{deliverable.Id}");
        Assert.Equal("Pending", untouched!.ApprovalStatus);
    }

    [Fact]
    public async Task ARoundOfAnotherDeliverable_IsNotFound_ThroughThisOnesUrl()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var mine = await SharedDeliverable(http, workspaceId, project.Id, "Mine");
        var theirs = await SharedDeliverable(http, workspaceId, project.Id, "Theirs");
        var round = (await (await http.PostAsJsonAsync(ApprovalsUrl(workspaceId, project.Id, theirs.Id), new { })).Content
            .ReadFromJsonAsync<DeliverableResponse>())!.Approvals.Single();

        var response = await http.PostAsync($"{ApprovalsUrl(workspaceId, project.Id, mine.Id)}/{round.Id}/withdraw", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Project_CountsWhatWaitsAndWhatCameBackWithChanges_ByEachDeliverablesLatestRound()
    {
        var (http, workspaceId, project) = await SignedInWithProject();
        var projectUrl = $"/api/workspaces/{workspaceId}/projects/{project.Id}";

        async Task<Guid> Ask(string title)
        {
            var deliverable = await SharedDeliverable(http, workspaceId, project.Id, title);
            var asked = await (await http.PostAsJsonAsync(ApprovalsUrl(workspaceId, project.Id, deliverable.Id), new { })).Content
                .ReadFromJsonAsync<DeliverableResponse>();
            return asked!.Approvals[0].Id;
        }

        await Ask("Waiting");
        await Decide(await Ask("Changes"), approve: false, message: "Bigger logo.");
        await Decide(await Ask("Approved"), approve: true);
        var withdrawn = await SharedDeliverable(http, workspaceId, project.Id, "Withdrawn");
        var withdrawnRound = (await (await http.PostAsJsonAsync(ApprovalsUrl(workspaceId, project.Id, withdrawn.Id), new { })).Content
            .ReadFromJsonAsync<DeliverableResponse>())!.Approvals.Single();
        await http.PostAsync($"{ApprovalsUrl(workspaceId, project.Id, withdrawn.Id)}/{withdrawnRound.Id}/withdraw", null);

        // Asked again after the changes: it counts as waiting, not twice.
        var again = await SharedDeliverable(http, workspaceId, project.Id, "Again");
        var againUrl = ApprovalsUrl(workspaceId, project.Id, again.Id);
        var againRound = (await (await http.PostAsJsonAsync(againUrl, new { })).Content.ReadFromJsonAsync<DeliverableResponse>())!.Approvals.Single();
        await Decide(againRound.Id, approve: false, message: "Redo.");
        await http.PostAsJsonAsync(againUrl, new { });

        // A deliverable taken back to a draft or archived is nobody's to answer, whatever its last round said.
        var draft = await SharedDeliverable(http, workspaceId, project.Id, "Back to draft");
        var draftRound = (await (await http.PostAsJsonAsync(ApprovalsUrl(workspaceId, project.Id, draft.Id), new { })).Content
            .ReadFromJsonAsync<DeliverableResponse>())!.Approvals.Single();
        await Decide(draftRound.Id, approve: false, message: "No.");
        await http.PostAsync($"{DeliverablesUrl(workspaceId, project.Id)}/{draft.Id}/unshare", null);

        var single = await http.GetFromJsonAsync<ProjectResponse>(projectUrl);
        var listed = (await http.GetFromJsonAsync<ProjectResponse[]>($"/api/workspaces/{workspaceId}/projects"))!.Single();

        Assert.Equal(new ProjectApprovalsResponse(Pending: 2, ChangesRequested: 1), single!.Approvals);
        Assert.Equal(single.Approvals, listed.Approvals);
    }

    [Fact]
    public async Task Project_WithoutApprovals_CountsZero()
    {
        var (_, _, project) = await SignedInWithProject();

        Assert.Equal(new ProjectApprovalsResponse(0, 0), project.Approvals);
    }

    [Fact]
    public async Task SignedOut_GetsUnauthorized()
    {
        var url = ApprovalsUrl(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var response = await factory.CreateClient().PostAsJsonAsync(url, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string DeliverablesUrl(Guid workspaceId, Guid projectId) =>
        $"/api/workspaces/{workspaceId}/projects/{projectId}/deliverables";

    private static string ApprovalsUrl(Guid workspaceId, Guid projectId, Guid deliverableId) =>
        $"{DeliverablesUrl(workspaceId, projectId)}/{deliverableId}/approvals";

    private async Task<(HttpClient Http, Guid WorkspaceId, ProjectResponse Project)> SignedInWithProject()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var clientResponse = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/clients", new { name = "Acme", email = NewEmail() });
        clientResponse.EnsureSuccessStatusCode();
        var client = (await clientResponse.Content.ReadFromJsonAsync<ClientResponse>())!;
        var projectResponse = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects", new { clientId = client.Id, name = "Website" });
        Assert.Equal(HttpStatusCode.Created, projectResponse.StatusCode);

        return (http, workspaceId, (await projectResponse.Content.ReadFromJsonAsync<ProjectResponse>())!);
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

    // The client's side of approvals has no endpoint yet, so the decision goes straight to the database.
    // Any existing account will do as the one who decided; the requester's is at hand.
    private async Task Decide(Guid approvalId, bool approve, string? message = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var round = await db.ApprovalRequests.SingleAsync(a => a.Id == approvalId);
        if (approve)
        {
            round.Approve(round.RequestedByUserId, message);
        }
        else
        {
            round.RequestChanges(round.RequestedByUserId, message);
        }

        await db.SaveChangesAsync();
    }

    private static async Task<DeliverableResponse> CreateDeliverable(HttpClient http, Guid workspaceId, Guid projectId, string title)
    {
        var response = await http.PostAsJsonAsync(DeliverablesUrl(workspaceId, projectId), new { title, url = "https://example.com/file" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DeliverableResponse>())!;
    }

    private static async Task<DeliverableResponse> SharedDeliverable(HttpClient http, Guid workspaceId, Guid projectId, string title)
    {
        var created = await CreateDeliverable(http, workspaceId, projectId, title);
        var shared = await http.PostAsync($"{DeliverablesUrl(workspaceId, projectId)}/{created.Id}/share", null);
        Assert.Equal(HttpStatusCode.OK, shared.StatusCode);
        return (await shared.Content.ReadFromJsonAsync<DeliverableResponse>())!;
    }
}
