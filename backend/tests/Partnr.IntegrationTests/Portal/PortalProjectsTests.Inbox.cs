using System.Net;
using System.Net.Http.Json;
using Partnr.Application.DTOs.Notifications;

namespace Partnr.IntegrationTests.Portal;

// The in-app side of the notifications: each account's own inbox.
public sealed partial class PortalProjectsTests
{
    [Fact]
    public async Task AskingForApproval_PutsAnUnreadNotificationInTheClientsInbox_WithWhereItLeads()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");

        await studio.RequestApproval(project, mockup, null);

        var inbox = (await elena.Http.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!;
        Assert.Equal(1, inbox.Unread);
        var item = Assert.Single(inbox.Items);
        Assert.Equal("Studio asked for your approval: Mockup", item.Title);
        Assert.Equal($"/w/{studio.WorkspaceId}/projects/{project.Id}?tab=deliverables", item.Path);
        Assert.Equal("Studio", item.WorkspaceName);
        Assert.False(item.IsRead);
        Assert.Equal(1, (await elena.Http.GetFromJsonAsync<UnreadCountResponse>("/api/notifications/unread-count"))!.Unread);
    }

    [Fact]
    public async Task TheClientsAnswer_ReachesTheFreelancersInbox_NextToTheOneAboutTheInvitation()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var round = await studio.RequestApproval(project, mockup, null);

        Assert.Equal(HttpStatusCode.OK, (await elena.Http.PostAsJsonAsync(ApprovalUrl(studio, project, mockup, round.Id, "approve"), new { })).StatusCode);

        var inbox = (await studio.Http.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!;
        Assert.Equal(["Elena approved Mockup", "Elena joined your client portal"], inbox.Items.Select(i => i.Title));
        Assert.Equal($"/w/{studio.WorkspaceId}/projects/{project.Id}?tab=deliverables", inbox.Items[0].Path);
        Assert.Equal(2, inbox.Unread);
    }

    [Fact]
    public async Task MarkRead_AndMarkAllRead_ClearTheUnreadCount()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var first = await studio.RequestApproval(project, mockup, null);
        Assert.Equal(HttpStatusCode.OK, (await elena.Http.PostAsJsonAsync(ApprovalUrl(studio, project, mockup, first.Id, "request-changes"), new { message = "Bigger logo." })).StatusCode);
        await studio.RequestApproval(project, mockup, "Once more.");
        var inbox = (await studio.Http.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!;
        Assert.Equal(2, inbox.Unread);

        var read = await studio.Http.PostAsync($"/api/notifications/{inbox.Items[0].Id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        Assert.Equal(1, (await studio.Http.GetFromJsonAsync<UnreadCountResponse>("/api/notifications/unread-count"))!.Unread);

        var all = await studio.Http.PostAsync("/api/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.NoContent, all.StatusCode);
        var after = (await studio.Http.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!;
        Assert.Equal(0, after.Unread);
        Assert.All(after.Items, i => Assert.True(i.IsRead));

        // The client's own inbox was not touched by any of this.
        Assert.Equal(2, (await elena.Http.GetFromJsonAsync<UnreadCountResponse>("/api/notifications/unread-count"))!.Unread);
    }

    [Fact]
    public async Task ANotificationOfSomeoneElse_IsNotFound_AndStaysUnread()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        await studio.RequestApproval(project, mockup, null);
        var theirs = (await elena.Http.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!.Items.Single();

        var response = await studio.Http.PostAsync($"/api/notifications/{theirs.Id}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await studio.Http.PostAsync($"/api/notifications/{Guid.NewGuid()}/read", null)).StatusCode);
        Assert.False((await elena.Http.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!.Items.Single().IsRead);
    }

    [Fact]
    public async Task ARequestTakenBack_LeavesTheInbox_EvenAfterItsEmailWentOut()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var round = await studio.RequestApproval(project, mockup, null);
        await EmailsTo(elena.Email, "asked for your approval"); // the email is out

        var withdrawn = await studio.Http.PostAsync($"{studio.DeliverablesUrl(project)}/{mockup.Id}/approvals/{round.Id}/withdraw", null);
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);

        var inbox = (await elena.Http.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!;
        Assert.Empty(inbox.Items);
        Assert.Equal(0, inbox.Unread);
    }

    [Fact]
    public async Task TheInbox_NeedsASession()
    {
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/notifications/unread-count")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/notifications/read-all", null)).StatusCode);
    }
}
