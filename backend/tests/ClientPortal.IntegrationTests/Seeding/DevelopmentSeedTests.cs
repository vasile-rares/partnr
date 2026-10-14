using System.Net;
using System.Net.Http.Json;
using ClientPortal.Application.DTOs.Auth;
using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.DTOs.Invitations;
using ClientPortal.Application.DTOs.Workspaces;
using ClientPortal.Infrastructure;

namespace ClientPortal.IntegrationTests.Seeding;

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

        // The client who accepted shows the account's details; the other one waits for an invitation that works.
        var elena = Assert.Single(all, c => c.Portal.Status == "Active");
        Assert.Equal("Elena Client", elena.Name);
        Assert.Equal("Elena Design", elena.Company);
        var pat = Assert.Single(all, c => c.Name == "Pat Pending");
        Assert.Equal("Invited", pat.Portal.Status);
        Assert.True(pat.DetailsLocked);
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
    }

    private async Task<HttpClient> SignIn(string email)
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("/api/auth/login", new { email, password = DevelopmentSeed.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return http;
    }
}
