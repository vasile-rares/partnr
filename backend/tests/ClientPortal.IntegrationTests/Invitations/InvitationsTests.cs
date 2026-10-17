using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ClientPortal.Application.DTOs.Auth;
using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.DTOs.Invitations;
using ClientPortal.Application.DTOs.Portal;
using ClientPortal.Application.DTOs.Workspaces;
using ClientPortal.Domain.Enums;
using ClientPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.IntegrationTests.Invitations;

[Collection(ApiCollection.Name)]
public sealed partial class InvitationsTests(ApiFactory factory)
{
    private const string Password = "correct horse";

    [Fact]
    public async Task Invite_EmailsALinkToTheClientAddress_AndTheClientShowsAsInvited()
    {
        var (studio, workspaceId, client) = await StudioWithClient();

        var response = await studio.PostAsync($"/api/workspaces/{workspaceId}/clients/{client.Id}/invitation", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invited = await response.Content.ReadFromJsonAsync<ClientResponse>();
        Assert.Equal("Invited", invited!.Portal.Status);
        Assert.NotNull(invited.Portal.ExpiresAtUtc);
        Assert.True(invited.DetailsLocked);

        var message = Assert.Single(factory.Emails.SentTo(client.Email));
        Assert.Contains("http://localhost:3000/invite/", message.TextBody);
        Assert.Contains(client.Email, message.TextBody);
    }

    [Fact]
    public async Task Preview_ShowsWhatTheLinkIsFor_WithoutSigningIn()
    {
        var (studio, workspaceId, client) = await StudioWithClient();
        var token = await Invite(studio, workspaceId, client);

        var anonymous = factory.CreateClient();
        var preview = await anonymous.GetFromJsonAsync<InvitationPreviewResponse>($"/api/invitations/{token}");

        Assert.Equal("Studio", preview!.WorkspaceName);
        Assert.Equal(client.Email, preview.Email);
        Assert.Equal("Pending", preview.Status);
        Assert.False(preview.AccountExists);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/invitations/not-a-real-token")).StatusCode);
    }

    [Fact]
    public async Task Register_CreatesTheAccountWithTheInvitedEmail_SignsIn_AndTheClientBecomesActive()
    {
        var (studio, workspaceId, client) = await StudioWithClient("Elena (card)");
        var token = await Invite(studio, workspaceId, client);
        var newClient = factory.CreateClient();

        var response = await newClient.PostAsJsonAsync(
            $"/api/invitations/{token}/register",
            new { fullName = "Elena Ionescu", password = Password, phone = "0722", company = "Elena Design" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var accepted = await response.Content.ReadFromJsonAsync<AcceptedInvitationResponse>();
        Assert.Equal(workspaceId, accepted!.WorkspaceId);

        var me = await newClient.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.Equal(client.Email, me!.Email);
        Assert.Equal("Elena Ionescu", me.FullName);
        Assert.Equal("Elena Design", me.Company);

        var seen = await studio.GetFromJsonAsync<ClientResponse>($"/api/workspaces/{workspaceId}/clients/{client.Id}");
        Assert.Equal("Active", seen!.Portal.Status);
        Assert.Equal("Elena Ionescu", seen.Name);
        Assert.Equal("0722", seen.Phone);
        Assert.True(seen.DetailsLocked);
    }

    [Fact]
    public async Task Accept_AnExistingAccountWithTheInvitedEmail_LinksTheClient()
    {
        var (studio, workspaceId, client) = await StudioWithClient();
        var token = await Invite(studio, workspaceId, client);
        var person = factory.CreateClient();
        await Register(person, client.Email);

        var preview = await person.GetFromJsonAsync<InvitationPreviewResponse>($"/api/invitations/{token}");
        var accept = await person.PostAsync($"/api/invitations/{token}/accept", null);

        Assert.True(preview!.AccountExists);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var seen = await studio.GetFromJsonAsync<ClientResponse>($"/api/workspaces/{workspaceId}/clients/{client.Id}");
        Assert.Equal("Active", seen!.Portal.Status);
    }

    [Fact]
    public async Task Register_WhenAnAccountWithThatEmailAlreadyExists_ReturnsConflict()
    {
        var (studio, workspaceId, client) = await StudioWithClient();
        var token = await Invite(studio, workspaceId, client);
        await Register(factory.CreateClient(), client.Email);

        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/invitations/{token}/register",
            new { fullName = "Someone", password = Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Accept_WithAnotherEmail_ReturnsForbidden_AndAnonymousReturnsUnauthorized()
    {
        var (studio, workspaceId, client) = await StudioWithClient();
        var token = await Invite(studio, workspaceId, client);
        var other = factory.CreateClient();
        await Register(other, NewEmail());

        var wrongAccount = await other.PostAsync($"/api/invitations/{token}/accept", null);
        var anonymous = await factory.CreateClient().PostAsync($"/api/invitations/{token}/accept", null);

        Assert.Equal(HttpStatusCode.Forbidden, wrongAccount.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var seen = await studio.GetFromJsonAsync<ClientResponse>($"/api/workspaces/{workspaceId}/clients/{client.Id}");
        Assert.Equal("Invited", seen!.Portal.Status);
    }

    [Fact]
    public async Task Accept_AsAMemberOfTheWorkspace_ReturnsConflict()
    {
        var (studio, workspaceId) = await SignedInWithWorkspace();
        var (member, memberId, memberEmail) = await SignedInWithEmail();
        await AddMember(workspaceId, memberId);
        var card = await RegisterClient(studio, workspaceId, "A member", memberEmail);
        var token = await Invite(studio, workspaceId, card);

        var response = await member.PostAsync($"/api/invitations/{token}/accept", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var seen = await studio.GetFromJsonAsync<ClientResponse>($"/api/workspaces/{workspaceId}/clients/{card.Id}");
        Assert.Equal("Invited", seen!.Portal.Status);
    }

    [Fact]
    public async Task Resending_ReplacesTheLink_SoTheOldOneStopsWorking()
    {
        var (studio, workspaceId, client) = await StudioWithClient();
        var first = await Invite(studio, workspaceId, client);
        var second = await Invite(studio, workspaceId, client);

        var withOld = await factory.CreateClient().PostAsJsonAsync(
            $"/api/invitations/{first}/register", new { fullName = "Elena", password = Password });
        var withNew = await factory.CreateClient().PostAsJsonAsync(
            $"/api/invitations/{second}/register", new { fullName = "Elena", password = Password });

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.Conflict, withOld.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withNew.StatusCode);
    }

    [Fact]
    public async Task Cancel_StopsTheLink_AndUnlocksTheDetails()
    {
        var (studio, workspaceId, client) = await StudioWithClient();
        var token = await Invite(studio, workspaceId, client);

        var cancel = await studio.DeleteAsync($"/api/workspaces/{workspaceId}/clients/{client.Id}/invitation");
        var cancelled = await cancel.Content.ReadFromJsonAsync<ClientResponse>();
        var preview = await factory.CreateClient().GetFromJsonAsync<InvitationPreviewResponse>($"/api/invitations/{token}");
        var register = await factory.CreateClient().PostAsJsonAsync(
            $"/api/invitations/{token}/register", new { fullName = "Elena", password = Password });
        var correction = await studio.PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/clients/{client.Id}", new { name = "Elena", email = NewEmail() });

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal("NotInvited", cancelled!.Portal.Status);
        Assert.False(cancelled.DetailsLocked);
        Assert.Equal("Cancelled", preview!.Status);
        Assert.Equal(HttpStatusCode.Conflict, register.StatusCode);
        Assert.Equal(HttpStatusCode.OK, correction.StatusCode);
    }

    [Fact]
    public async Task WhileInvited_OnlyTheNotesCanBeEdited()
    {
        var (studio, workspaceId, client) = await StudioWithClient("Elena");
        await Invite(studio, workspaceId, client);
        var url = $"/api/workspaces/{workspaceId}/clients/{client.Id}";

        var rename = await studio.PutAsJsonAsync(url, new { name = "Renamed", email = client.Email });
        var notesOnly = await studio.PutAsJsonAsync(url, new { name = "Elena", email = client.Email, notes = "Call on Monday." });

        Assert.Equal(HttpStatusCode.Conflict, rename.StatusCode);
        Assert.Equal(HttpStatusCode.OK, notesOnly.StatusCode);
        var seen = await studio.GetFromJsonAsync<ClientResponse>(url);
        Assert.Equal("Elena", seen!.Name);
        Assert.Equal("Call on Monday.", seen.Notes);
    }

    [Fact]
    public async Task Archiving_CancelsTheInvitation_AndAnArchivedClientCannotBeInvited()
    {
        var (studio, workspaceId, client) = await StudioWithClient();
        var token = await Invite(studio, workspaceId, client);

        var archive = await studio.PostAsync($"/api/workspaces/{workspaceId}/clients/{client.Id}/archive", null);
        var preview = await factory.CreateClient().GetFromJsonAsync<InvitationPreviewResponse>($"/api/invitations/{token}");
        var inviteAgain = await studio.PostAsync($"/api/workspaces/{workspaceId}/clients/{client.Id}/invitation", null);

        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        Assert.Equal("Cancelled", preview!.Status);
        Assert.Equal(HttpStatusCode.Conflict, inviteAgain.StatusCode);
    }

    [Fact]
    public async Task AnExpiredInvitation_CannotBeUsed_ShowsAsExpired_AndUnlocksTheDetails()
    {
        var (studio, workspaceId, client) = await StudioWithClient();
        var token = await Invite(studio, workspaceId, client);
        await Expire(client.Id);

        var preview = await factory.CreateClient().GetFromJsonAsync<InvitationPreviewResponse>($"/api/invitations/{token}");
        var register = await factory.CreateClient().PostAsJsonAsync(
            $"/api/invitations/{token}/register", new { fullName = "Elena", password = Password });
        var seen = await studio.GetFromJsonAsync<ClientResponse>($"/api/workspaces/{workspaceId}/clients/{client.Id}");
        var correction = await studio.PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/clients/{client.Id}", new { name = "Elena", email = NewEmail() });
        var resend = await studio.PostAsync($"/api/workspaces/{workspaceId}/clients/{client.Id}/invitation", null);

        Assert.Equal("Expired", preview!.Status);
        Assert.Equal(HttpStatusCode.Conflict, register.StatusCode);
        Assert.Equal("Expired", seen!.Portal.Status);
        Assert.False(seen.DetailsLocked);
        Assert.Equal(HttpStatusCode.OK, correction.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
    }

    [Fact]
    public async Task SomeoneOutsideTheWorkspace_CannotInviteOrCancel()
    {
        var (studio, workspaceId, client) = await StudioWithClient();
        var (stranger, _, _) = await SignedInWithEmail();

        var invite = await stranger.PostAsync($"/api/workspaces/{workspaceId}/clients/{client.Id}/invitation", null);
        var cancel = await stranger.DeleteAsync($"/api/workspaces/{workspaceId}/clients/{client.Id}/invitation");

        Assert.Equal(HttpStatusCode.NotFound, invite.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Empty(factory.Emails.SentTo(client.Email));
        var seen = await studio.GetFromJsonAsync<ClientResponse>($"/api/workspaces/{workspaceId}/clients/{client.Id}");
        Assert.Equal("NotInvited", seen!.Portal.Status);
    }

    [Fact]
    public async Task AMember_CanInvite()
    {
        var (_, workspaceId, client) = await StudioWithClient();
        var (member, memberId, _) = await SignedInWithEmail();
        await AddMember(workspaceId, memberId);

        var response = await member.PostAsync($"/api/workspaces/{workspaceId}/clients/{client.Id}/invitation", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Portal_ListsTheWorkspaceOfAClientWhoAccepted_ButTheClientIsNotAMember()
    {
        var (studio, workspaceId, client) = await StudioWithClient();
        var token = await Invite(studio, workspaceId, client);
        var elena = factory.CreateClient();
        await elena.PostAsJsonAsync($"/api/invitations/{token}/register", new { fullName = "Elena", password = Password });

        var portal = await elena.GetFromJsonAsync<PortalWorkspaceResponse[]>("/api/portal/workspaces");
        var asMember = await elena.GetAsync($"/api/workspaces/{workspaceId}");
        var theirClients = await elena.GetAsync($"/api/workspaces/{workspaceId}/clients");
        var memberships = await elena.GetFromJsonAsync<WorkspaceResponse[]>("/api/workspaces");

        var shown = Assert.Single(portal!);
        Assert.Equal(workspaceId, shown.Id);
        Assert.Equal("Studio", shown.Name);
        Assert.Equal(HttpStatusCode.NotFound, asMember.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, theirClients.StatusCode);
        Assert.Empty(memberships!);
    }

    [Fact]
    public async Task Portal_ArchivingTheClientTakesTheAccessAway_AndRestoringGivesItBack()
    {
        var (studio, workspaceId, client) = await StudioWithClient();
        var token = await Invite(studio, workspaceId, client);
        var elena = factory.CreateClient();
        await elena.PostAsJsonAsync($"/api/invitations/{token}/register", new { fullName = "Elena", password = Password });

        await studio.PostAsync($"/api/workspaces/{workspaceId}/clients/{client.Id}/archive", null);
        var whileArchived = await elena.GetFromJsonAsync<PortalWorkspaceResponse[]>("/api/portal/workspaces");
        await studio.PostAsync($"/api/workspaces/{workspaceId}/clients/{client.Id}/restore", null);
        var afterRestore = await elena.GetFromJsonAsync<PortalWorkspaceResponse[]>("/api/portal/workspaces");

        Assert.Empty(whileArchived!);
        Assert.Equal(workspaceId, Assert.Single(afterRestore!).Id);
    }

    [Fact]
    public async Task Portal_IsEmptyForSomeoneWhoIsNotAClient_AndNeedsSignIn()
    {
        var (stranger, _, _) = await SignedInWithEmail();
        var (studio, workspaceId, client) = await StudioWithClient();
        await Invite(studio, workspaceId, client); // invited, but nobody accepted

        var list = await stranger.GetFromJsonAsync<PortalWorkspaceResponse[]>("/api/portal/workspaces");
        var anonymous = await factory.CreateClient().GetAsync("/api/portal/workspaces");

        Assert.Empty(list!);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    [GeneratedRegex("/invite/([A-Za-z0-9_-]+)")]
    private static partial Regex InviteLink();

    private async Task<(HttpClient Http, Guid WorkspaceId)> SignedInWithWorkspace()
    {
        var (http, _, _) = await SignedInWithEmail();
        var response = await http.PostAsJsonAsync("/api/workspaces", new { name = "Studio" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();

        return (http, workspace!.Id);
    }

    private async Task<(HttpClient Http, Guid UserId, string Email)> SignedInWithEmail()
    {
        var http = factory.CreateClient();
        var email = NewEmail();
        var user = await Register(http, email);

        return (http, user.Id, email);
    }

    private static async Task<CurrentUserResponse> Register(HttpClient http, string email)
    {
        var response = await http.PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = "Test User" });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CurrentUserResponse>())!;
    }

    private async Task<(HttpClient Studio, Guid WorkspaceId, ClientResponse Client)> StudioWithClient(string name = "Elena")
    {
        var (studio, workspaceId) = await SignedInWithWorkspace();

        return (studio, workspaceId, await RegisterClient(studio, workspaceId, name, NewEmail()));
    }

    private static async Task<ClientResponse> RegisterClient(HttpClient studio, Guid workspaceId, string name, string email)
    {
        var response = await studio.PostAsJsonAsync($"/api/workspaces/{workspaceId}/clients", new { name, email });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<ClientResponse>())!;
    }

    private async Task<string> Invite(HttpClient studio, Guid workspaceId, ClientResponse client)
    {
        var before = factory.Emails.SentTo(client.Email).Count;
        var response = await studio.PostAsync($"/api/workspaces/{workspaceId}/clients/{client.Id}/invitation", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sent = factory.Emails.SentTo(client.Email);
        Assert.Equal(before + 1, sent.Count);

        return InviteLink().Match(sent[^1].TextBody).Groups[1].Value;
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

    // The clock is real, so an invitation is expired by moving its expiry into the past.
    private async Task Expire(Guid clientId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.ClientInvitations
            .Where(i => i.ClientId == clientId)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
    }
}
