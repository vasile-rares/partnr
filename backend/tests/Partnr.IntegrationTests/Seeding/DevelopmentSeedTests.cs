using System.Net;
using System.Net.Http.Json;
using Partnr.Application.DTOs.Auth;
using Partnr.Application.DTOs.Clients;
using Partnr.Application.DTOs.Deliverables;
using Partnr.Application.DTOs.ProjectUpdates;
using Partnr.Application.DTOs.Invitations;
using Partnr.Application.DTOs.Messages;
using Partnr.Application.DTOs.Projects;
using Partnr.Application.DTOs.Workspaces;
using Partnr.Infrastructure;

namespace Partnr.IntegrationTests.Seeding;

[Collection(ApiCollection.Name)]
public sealed class DevelopmentSeedTests(ApiFactory factory)
{
    [Fact]
    public async Task Seeding_Twice_ChangesNothing_AndTheDemoAccountsCanSignInAndSeeTheirData()
    {
        await factory.Services.SeedDevelopmentDataAsync();
        await factory.Services.SeedDevelopmentDataAsync();

        var owner = await SignIn(DevelopmentSeed.OwnerEmail);
        var workspaces = await owner.GetFromJsonAsync<WorkspaceResponse[]>("/api/workspaces");

        // Two workspaces, not four: the second run did nothing, and "Other Studio" belongs to someone else.
        Assert.Equal(["Studio Pop", "Side Project"], workspaces!.Select(w => w.Name));

        var studio = workspaces![0];
        var active = await owner.GetFromJsonAsync<ClientResponse[]>($"/api/workspaces/{studio.Id}/clients");
        var all = await owner.GetFromJsonAsync<ClientResponse[]>($"/api/workspaces/{studio.Id}/clients?includeArchived=true");
        Assert.Equal(9, active!.Length);
        Assert.Equal(11, all!.Length);

        var elena = Assert.Single(all, c => c.Portal.Status == "Active");
        Assert.Equal("Elena Client", elena.Name);
        Assert.Equal("Elena Design", elena.Company);
        var pat = Assert.Single(all, c => c.Name == "Pat Pending");
        Assert.Equal("Invited", pat.Portal.Status);
        Assert.True(pat.DetailsLocked);

        var projects = await owner.GetFromJsonAsync<ProjectResponse[]>($"/api/workspaces/{studio.Id}/projects?clientId={elena.Id}");
        Assert.Equal(["Active", "Planning"], projects!.Select(p => p.Status).Order());
        Assert.All(projects!, p => Assert.Equal("Elena Client", p.Client.DisplayName));

        var website = Assert.Single(projects!, p => p.Status == "Active");
        var deliverables = await owner.GetFromJsonAsync<DeliverableResponse[]>($"/api/workspaces/{studio.Id}/projects/{website.Id}/deliverables");
        Assert.Equal(["Draft", "Shared"], deliverables!.Select(d => d.Status).Order());
        var mockups = Assert.Single(deliverables!, d => d.Status == "Shared");
        Assert.Equal("Pending", mockups.ApprovalStatus);
        Assert.Equal(["Pending", "ChangesRequested"], mockups.Approvals.Select(a => a.Status));
        var updates = await owner.GetFromJsonAsync<ProjectUpdateResponse[]>($"/api/workspaces/{studio.Id}/projects/{website.Id}/updates");
        Assert.Equal(["Draft", "Shared"], updates!.Select(u => u.Status).Order());

        var preview = await factory.CreateClient()
            .GetFromJsonAsync<InvitationPreviewResponse>($"/api/invitations/{DevelopmentSeed.PendingInvitationToken}");
        Assert.Equal("Pending", preview!.Status);
        Assert.Equal("Studio Pop", preview.WorkspaceName);
        await SignIn(DevelopmentSeed.ClientEmail);

        var ownerProfile = await owner.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.Equal("Studio Pop SRL", ownerProfile!.Company);
        Assert.NotNull(ownerProfile.Phone);

        var member = await SignIn(DevelopmentSeed.MemberEmail);
        var asMember = await member.GetFromJsonAsync<WorkspaceResponse>($"/api/workspaces/{studio.Id}");
        Assert.Equal("Member", asMember!.Role);

        // The website's conversation: one deleted and one edited message, and each person a different unread count.
        var conversation = await owner.GetFromJsonAsync<ConversationResponse>($"/api/workspaces/{studio.Id}/conversations/{website.Id}");
        Assert.Equal(7, conversation!.Messages.Count);
        Assert.Single(conversation.Messages, m => m.IsDeleted);
        Assert.Single(conversation.Messages, m => m.EditedAtUtc is not null && !m.IsDeleted);
        Assert.Equal(1, await Unread(owner, $"/api/workspaces/{studio.Id}/conversations/unread-count"));
        Assert.Equal(5, await Unread(member, $"/api/workspaces/{studio.Id}/conversations/unread-count"));
        Assert.Equal(1, await Unread(await SignIn(DevelopmentSeed.ClientEmail), $"/api/portal/workspaces/{studio.Id}/conversations/unread-count"));
    }

    private static async Task<int> Unread(HttpClient http, string url) =>
        (await http.GetFromJsonAsync<UnreadMessagesResponse>(url))!.Unread;

    private async Task<HttpClient> SignIn(string email)
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("/api/auth/login", new { email, password = DevelopmentSeed.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return http;
    }
}
