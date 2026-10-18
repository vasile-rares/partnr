using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClientPortal.Application.DTOs.Deliverables;
using ClientPortal.Application.DTOs.Portal;
using ClientPortal.Application.DTOs.Projects;

namespace ClientPortal.IntegrationTests.Portal;

// The client's answers to approval requests. Shares the helpers (and the sessions) of the portal tests.
public sealed partial class PortalProjectsTests
{
    [Fact]
    public async Task Client_SeesTheOpenRequest_CountsItAsWaiting_AndApprovesIt()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var round = await studio.RequestApproval(project, mockup, "Check the mobile layout.");

        var before = (await elena.Http.GetFromJsonAsync<PortalProjectResponse>($"/api/portal/projects/{project.Id}"))!;
        var list = (await elena.Http.GetFromJsonAsync<PortalProjectSummaryResponse[]>("/api/portal/projects"))!;

        var approval = Assert.Single(before.Deliverables).Approval;
        Assert.Equal("Pending", approval.Status);
        Assert.Equal((round.Id, "Check the mobile layout."), (approval.Pending!.Id, approval.Pending.Note));
        Assert.Equal(1, before.AwaitingApproval);
        Assert.Equal(1, Assert.Single(list).AwaitingApproval);

        var approved = await elena.Http.PostAsJsonAsync(ApprovalUrl(project, mockup, round.Id, "approve"), new { message = "Looks great." });

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var answer = (await approved.Content.ReadFromJsonAsync<PortalDeliverableResponse>())!.Approval;
        Assert.Equal("Approved", answer.Status);
        Assert.Null(answer.Pending);
        var decided = Assert.Single(answer.History);
        Assert.Equal(("Approved", "Check the mobile layout.", "Looks great."), (decided.Decision, decided.Note, decided.Message));

        var after = (await elena.Http.GetFromJsonAsync<PortalProjectResponse>($"/api/portal/projects/{project.Id}"))!;
        Assert.Equal(0, after.AwaitingApproval);
        Assert.Equal(0, (await elena.Http.GetFromJsonAsync<PortalProjectSummaryResponse[]>("/api/portal/projects"))![0].AwaitingApproval);

        var asFreelancer = await studio.Http.GetFromJsonAsync<DeliverableResponse>($"{studio.DeliverablesUrl(project)}/{mockup.Id}");
        Assert.Equal("Approved", asFreelancer!.ApprovalStatus);
        Assert.Equal("Looks great.", asFreelancer.Approvals.Single().Message);
    }

    [Fact]
    public async Task RequestChanges_NeedsAMessage_AndThenTheFreelancerSeesIt_AndCanAskAgainAfterEditing()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var round = await studio.RequestApproval(project, mockup, null);
        var url = ApprovalUrl(project, mockup, round.Id, "request-changes");

        var blank = await elena.Http.PostAsJsonAsync(url, new { message = "  " });
        var missing = await elena.Http.PostAsJsonAsync(url, new { });
        var changes = await elena.Http.PostAsJsonAsync(url, new { message = "The logo is too small." });

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, changes.StatusCode);

        var seen = (await studio.Http.GetFromJsonAsync<DeliverableResponse>($"{studio.DeliverablesUrl(project)}/{mockup.Id}"))!;
        Assert.Equal(("ChangesRequested", "The logo is too small."), (seen.ApprovalStatus, seen.Approvals.Single().Message));

        var edited = await studio.Http.PutAsJsonAsync($"{studio.DeliverablesUrl(project)}/{mockup.Id}", new { title = "Mockup v2", url = "https://example.com/v2" });
        var again = await studio.RequestApproval(project, mockup, "Logo is bigger now.");
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

        var view = (await elena.Http.GetFromJsonAsync<PortalProjectResponse>($"/api/portal/projects/{project.Id}"))!;
        var approval = view.Deliverables.Single().Approval;
        Assert.Equal(("Pending", again.Id), (approval.Status, approval.Pending!.Id));
        Assert.Equal("ChangesRequested", Assert.Single(approval.History).Decision);
    }

    [Fact]
    public async Task ARoundIsAnsweredOnce_AndAMessageOfTheWrongSizeIsRefused()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var round = await studio.RequestApproval(project, mockup, null);

        var tooLong = await elena.Http.PostAsJsonAsync(ApprovalUrl(project, mockup, round.Id, "approve"), new { message = new string('x', 2001) });
        var first = await elena.Http.PostAsJsonAsync(ApprovalUrl(project, mockup, round.Id, "approve"), new { });
        var second = await elena.Http.PostAsJsonAsync(ApprovalUrl(project, mockup, round.Id, "request-changes"), new { message = "Changed my mind." });

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var view = (await elena.Http.GetFromJsonAsync<PortalProjectResponse>($"/api/portal/projects/{project.Id}"))!;
        Assert.Equal("Approved", view.Deliverables.Single().Approval.Status);
    }

    [Fact]
    public async Task AWithdrawnRequest_NeverShowsToTheClient_AndCannotBeAnswered()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var round = await studio.RequestApproval(project, mockup, "Never mind.");
        await studio.Http.PostAsync($"{studio.DeliverablesUrl(project)}/{mockup.Id}/approvals/{round.Id}/withdraw", null);

        var view = (await elena.Http.GetFromJsonAsync<PortalProjectResponse>($"/api/portal/projects/{project.Id}"))!;
        var answer = await elena.Http.PostAsJsonAsync(ApprovalUrl(project, mockup, round.Id, "approve"), new { });

        var approval = view.Deliverables.Single().Approval;
        Assert.Equal(("None", 0), (approval.Status, view.AwaitingApproval));
        Assert.Null(approval.Pending);
        Assert.Empty(approval.History);
        Assert.Equal(HttpStatusCode.Conflict, answer.StatusCode);
    }

    [Fact]
    public async Task Unsharing_TakesTheRequestAway_AndTheClientCanNoLongerAnswerIt()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var round = await studio.RequestApproval(project, mockup, null);

        await studio.Http.PostAsync($"{studio.DeliverablesUrl(project)}/{mockup.Id}/unshare", null);
        var answer = await elena.Http.PostAsJsonAsync(ApprovalUrl(project, mockup, round.Id, "approve"), new { });
        var view = (await elena.Http.GetFromJsonAsync<PortalProjectResponse>($"/api/portal/projects/{project.Id}"))!;

        Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
        Assert.Empty(view.Deliverables);
        Assert.Equal(0, view.AwaitingApproval);
        Assert.Equal(0, (await elena.Http.GetFromJsonAsync<PortalProjectSummaryResponse[]>("/api/portal/projects"))![0].AwaitingApproval);
    }

    [Fact]
    public async Task AnotherClient_CannotAnswer_AndTheRoundStaysOpen()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var bram = await studio.AddClient("Bram");
        var round = await studio.RequestApproval(project, mockup, null);

        var approve = await bram.Http.PostAsJsonAsync(ApprovalUrl(project, mockup, round.Id, "approve"), new { });
        var changes = await bram.Http.PostAsJsonAsync(ApprovalUrl(project, mockup, round.Id, "request-changes"), new { message = "No." });
        var signedOut = await factory.CreateClient().PostAsJsonAsync(ApprovalUrl(project, mockup, round.Id, "approve"), new { });

        Assert.Equal(HttpStatusCode.NotFound, approve.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, changes.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
        var view = (await elena.Http.GetFromJsonAsync<PortalProjectResponse>($"/api/portal/projects/{project.Id}"))!;
        Assert.Equal("Pending", view.Deliverables.Single().Approval.Status);
    }

    [Fact]
    public async Task AProjectStillInPlanning_CannotBeAnsweredEvenWithAnOpenRequest()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var round = await studio.RequestApproval(project, mockup, null);
        await studio.SetStatus(project, "Planning");

        var answer = await elena.Http.PostAsJsonAsync(ApprovalUrl(project, mockup, round.Id, "approve"), new { });
        var list = (await elena.Http.GetFromJsonAsync<PortalProjectSummaryResponse[]>("/api/portal/projects"))!;

        Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
        Assert.Empty(list);
    }

    [Fact]
    public async Task PortalApproval_ShowsNoIdsOfTheWorkspacesSide_AndNoAccount()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var answered = await studio.RequestApproval(project, mockup, "First.");
        await elena.Http.PostAsJsonAsync(ApprovalUrl(project, mockup, answered.Id, "request-changes"), new { message = "Fix it." });
        await studio.RequestApproval(project, mockup, "Second.");

        using var body = JsonDocument.Parse(await elena.Http.GetStringAsync($"/api/portal/projects/{project.Id}"));

        var approval = body.RootElement.GetProperty("deliverables")[0].GetProperty("approval");
        Assert.Equal(["history", "pending", "status"], approval.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(["id", "note", "requestedAtUtc"], approval.GetProperty("pending").EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(
            ["decidedAtUtc", "decision", "message", "note", "requestedAtUtc"],
            approval.GetProperty("history")[0].EnumerateObject().Select(p => p.Name).Order());
    }

    private async Task<(StudioSession Studio, ClientSession Elena, ProjectResponse Project, DeliverableResponse Deliverable)> SharedDeliverable(string clientName)
    {
        var studio = await Studio();
        var elena = await studio.AddClient(clientName);
        var project = await studio.AddProject(elena, "Website");
        await studio.SetStatus(project, "Active");
        var mockup = await studio.AddDeliverable(project, "Mockup", null, share: true);

        return (studio, elena, project, mockup);
    }

    private static string ApprovalUrl(
        ProjectResponse project,
        DeliverableResponse deliverable,
        Guid approvalId,
        string action) =>
        $"/api/portal/projects/{project.Id}/deliverables/{deliverable.Id}/approvals/{approvalId}/{action}";
}
