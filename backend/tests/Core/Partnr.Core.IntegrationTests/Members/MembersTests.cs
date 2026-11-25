using System.Net;
using System.Net.Http.Json;
using Partnr.Core.Application.DTOs.Auth;
using Partnr.Core.Application.DTOs.Invitations;
using Partnr.Core.Application.DTOs.Meetings;
using Partnr.Core.Application.DTOs.Members;
using Partnr.Core.Application.DTOs.Workspaces;

namespace Partnr.Core.IntegrationTests.Members;

// The team, over HTTP with a real database: who may invite, remove and read, and what a new member can then do.
[Collection(ApiCollection.Name)]
public sealed class MembersTests(ApiFactory factory)
{
    private const string Password = "correct horse";

    [Fact]
    public async Task TheOwnerInvites_TheEmailCarriesTheToken_AndTheListShowsItAsPending()
    {
        var (owner, workspaceId) = await Owner();
        var email = NewEmail();

        var response = await owner.PostAsJsonAsync(InvitationsUrl(workspaceId), new { email = email.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<MemberInvitationResponse>();
        Assert.Equal((email, "Pending"), (created!.Email, created.Status));

        var sent = Assert.Single(factory.Recorded.MemberInvitationsTo(email));
        Assert.Equal("Studio", sent.WorkspaceName);
        Assert.False(string.IsNullOrEmpty(sent.Token));

        var list = await owner.GetFromJsonAsync<MemberInvitationResponse[]>(InvitationsUrl(workspaceId));
        Assert.Equal(created.Id, Assert.Single(list!).Id);
    }

    [Fact]
    public async Task AMemberCannotInviteListCancelOrRemove_AndAStrangerSeesNothing()
    {
        var (owner, workspaceId) = await Owner();
        var member = await JoinAs(owner, workspaceId);
        var invitation = await Invite(owner, workspaceId, NewEmail());
        var stranger = await SignedIn();

        Assert.Equal(HttpStatusCode.Forbidden, (await member.Http.PostAsJsonAsync(InvitationsUrl(workspaceId), new { email = NewEmail() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Http.GetAsync(InvitationsUrl(workspaceId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Http.DeleteAsync($"{InvitationsUrl(workspaceId)}/{invitation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Http.DeleteAsync($"{MembersUrl(workspaceId)}/{member.UserId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.GetAsync(MembersUrl(workspaceId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.PostAsJsonAsync(InvitationsUrl(workspaceId), new { email = NewEmail() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.GetAsync(InvitationsUrl(workspaceId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.DeleteAsync($"{MembersUrl(workspaceId)}/{member.UserId}")).StatusCode);

        // Nothing of the above changed anything.
        Assert.Single((await owner.GetFromJsonAsync<MemberInvitationResponse[]>(InvitationsUrl(workspaceId)))!);
        Assert.Equal(2, (await owner.GetFromJsonAsync<MemberResponse[]>(MembersUrl(workspaceId)))!.Length);
    }

    [Fact]
    public async Task AnInvalidEmail_IsABadRequest()
    {
        var (owner, workspaceId) = await Owner();

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(InvitationsUrl(workspaceId), new { email = "not-an-email" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(InvitationsUrl(workspaceId), new { email = "" })).StatusCode);
    }

    [Fact]
    public async Task TheLinkIsPreviewedWithoutSigningIn_AsATeamInvitation()
    {
        var (owner, workspaceId) = await Owner();
        var email = NewEmail();
        await Invite(owner, workspaceId, email);

        var preview = await factory.CreateClient().GetFromJsonAsync<InvitationPreviewResponse>($"/api/invitations/{TokenFor(email)}");

        Assert.Equal(("Studio", email, "Pending", "Member"), (preview!.WorkspaceName, preview.Email, preview.Status, preview.Kind));
        Assert.False(preview.AccountExists);
    }

    [Fact]
    public async Task RegisteringThroughTheLink_JoinsTheTeam_SignsIn_AndTheNewMemberSeesTheWorkspace()
    {
        var (owner, workspaceId) = await Owner();
        var email = NewEmail();
        await Invite(owner, workspaceId, email);
        var newcomer = factory.CreateClient();

        var response = await newcomer.PostAsJsonAsync($"/api/invitations/{TokenFor(email)}/register", new { fullName = "Mihai Member", password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(workspaceId, (await response.Content.ReadFromJsonAsync<AcceptedInvitationResponse>())!.WorkspaceId);
        Assert.Equal(email, (await newcomer.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me"))!.Email);

        var mine = await newcomer.GetFromJsonAsync<WorkspaceResponse[]>("/api/workspaces");
        Assert.Equal("Member", Assert.Single(mine!, w => w.Id == workspaceId).Role);
        Assert.Equal(HttpStatusCode.OK, (await newcomer.GetAsync($"/api/workspaces/{workspaceId}/clients")).StatusCode);

        var members = await owner.GetFromJsonAsync<MemberResponse[]>(MembersUrl(workspaceId));
        Assert.NotNull(members);
        Assert.Equal(["Owner", "Member"], members.Select(m => m.Role));
        Assert.Equal("Mihai Member", members[1].Name);
        Assert.Empty((await owner.GetFromJsonAsync<MemberInvitationResponse[]>(InvitationsUrl(workspaceId)))!);
    }

    [Fact]
    public async Task AnExistingAccountAcceptsWithItsOwnEmail_AndAnotherAccountIsRefused()
    {
        var (owner, workspaceId) = await Owner();
        var (account, _, email) = await SignedInWithEmail();
        await Invite(owner, workspaceId, email);
        var other = await SignedIn();

        Assert.Equal(HttpStatusCode.Forbidden, (await other.Http.PostAsync($"/api/invitations/{TokenFor(email)}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await account.PostAsync($"/api/invitations/{TokenFor(email)}/accept", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await account.GetAsync(MembersUrl(workspaceId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.GetAsync(MembersUrl(workspaceId))).StatusCode);
        // Used once.
        Assert.Equal(HttpStatusCode.Conflict, (await account.PostAsync($"/api/invitations/{TokenFor(email)}/accept", null)).StatusCode);
    }

    [Fact]
    public async Task InvitingSomeoneAlreadyOnTheTeam_IsAConflict_AndSendsNothing()
    {
        var (owner, workspaceId) = await Owner();
        var member = await JoinAs(owner, workspaceId);

        var response = await owner.PostAsJsonAsync(InvitationsUrl(workspaceId), new { email = member.Email });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(factory.Recorded.MemberInvitationsTo(member.Email).Skip(1));
    }

    [Fact]
    public async Task SendingAgain_ReplacesTheOpenInvitation_SoTheOldLinkStopsWorking()
    {
        var (owner, workspaceId) = await Owner();
        var email = NewEmail();
        await Invite(owner, workspaceId, email);
        var firstToken = TokenFor(email);

        await Invite(owner, workspaceId, email);

        var anonymous = factory.CreateClient();
        Assert.Equal("Cancelled", (await anonymous.GetFromJsonAsync<InvitationPreviewResponse>($"/api/invitations/{firstToken}"))!.Status);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await anonymous.PostAsJsonAsync($"/api/invitations/{firstToken}/register", new { fullName = "X", password = Password })).StatusCode);
        Assert.Single((await owner.GetFromJsonAsync<MemberInvitationResponse[]>(InvitationsUrl(workspaceId)))!);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/invitations/{TokenFor(email)}")).StatusCode);
    }

    [Fact]
    public async Task Cancelling_KillsTheLink_AndCancellingAgainIsFine()
    {
        var (owner, workspaceId) = await Owner();
        var email = NewEmail();
        var invitation = await Invite(owner, workspaceId, email);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"{InvitationsUrl(workspaceId)}/{invitation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"{InvitationsUrl(workspaceId)}/{invitation.Id}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await factory.CreateClient().PostAsJsonAsync($"/api/invitations/{TokenFor(email)}/register", new { fullName = "X", password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync($"{InvitationsUrl(workspaceId)}/{Guid.NewGuid()}")).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<MemberInvitationResponse[]>(InvitationsUrl(workspaceId)))!);
    }

    [Fact]
    public async Task AnInvitationOfAnotherWorkspace_CannotBeCancelledFromThisOne()
    {
        var (owner, workspaceId) = await Owner();
        var (otherOwner, otherWorkspaceId) = await Owner();
        var foreign = await Invite(otherOwner, otherWorkspaceId, NewEmail());

        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync($"{InvitationsUrl(workspaceId)}/{foreign.Id}")).StatusCode);
        Assert.Single((await otherOwner.GetFromJsonAsync<MemberInvitationResponse[]>(InvitationsUrl(otherWorkspaceId)))!);
    }

    [Fact]
    public async Task Removing_TakesTheMemberOut_AndTheyLoseTheWorkspaceAtOnce_ButCanBeInvitedAgain()
    {
        var (owner, workspaceId) = await Owner();
        var member = await JoinAs(owner, workspaceId);
        Assert.Equal(HttpStatusCode.OK, (await member.Http.GetAsync($"/api/workspaces/{workspaceId}/clients")).StatusCode);

        var removed = await owner.DeleteAsync($"{MembersUrl(workspaceId)}/{member.UserId}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.Http.GetAsync($"/api/workspaces/{workspaceId}/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.Http.GetAsync(MembersUrl(workspaceId))).StatusCode);
        Assert.Empty((await member.Http.GetFromJsonAsync<WorkspaceResponse[]>("/api/workspaces"))!);
        Assert.Single((await owner.GetFromJsonAsync<MemberResponse[]>(MembersUrl(workspaceId)))!);

        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync(InvitationsUrl(workspaceId), new { email = member.Email })).StatusCode);
    }

    [Fact]
    public async Task TheOwnerCannotBeRemoved_AndAnUnknownMemberIsNotFound()
    {
        var (owner, workspaceId) = await Owner();
        var me = await owner.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync($"{MembersUrl(workspaceId)}/{me!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync($"{MembersUrl(workspaceId)}/{Guid.NewGuid()}")).StatusCode);
        Assert.Single((await owner.GetFromJsonAsync<MemberResponse[]>(MembersUrl(workspaceId)))!);
    }

    [Fact]
    public async Task RemovingIsRefusedWhileTheMemberOrganisesAnUpcomingMeeting_UntilItIsCancelled()
    {
        var (owner, workspaceId) = await Owner();
        var member = await JoinAs(owner, workspaceId);
        factory.Google.Connect(member.UserId);
        var client = await (await owner.PostAsJsonAsync($"/api/workspaces/{workspaceId}/clients", new { name = "Elena", email = NewEmail() })).Content.ReadFromJsonAsync<Partnr.Core.Application.DTOs.Clients.ClientResponse>();
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var scheduled = await member.Http.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/meetings",
            new { clientId = client!.Id, title = "Kick-off", startsAt = start, endsAt = start.AddHours(1) });
        Assert.Equal(HttpStatusCode.Created, scheduled.StatusCode);
        var meeting = await scheduled.Content.ReadFromJsonAsync<MeetingResponse>();

        var refused = await owner.DeleteAsync($"{MembersUrl(workspaceId)}/{member.UserId}");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("1 upcoming meeting", await refused.Content.ReadAsStringAsync());
        Assert.Equal(2, (await owner.GetFromJsonAsync<MemberResponse[]>(MembersUrl(workspaceId)))!.Length);

        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/workspaces/{workspaceId}/meetings/{meeting!.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"{MembersUrl(workspaceId)}/{member.UserId}")).StatusCode);
    }

    private static string MembersUrl(Guid workspaceId) => $"/api/workspaces/{workspaceId}/members";

    private static string InvitationsUrl(Guid workspaceId) => $"/api/workspaces/{workspaceId}/member-invitations";

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    // The plain token only exists in the event that carried it to the email.
    private string TokenFor(string email) => factory.Recorded.MemberInvitationsTo(email)[^1].Token;

    private async Task<MemberInvitationResponse> Invite(HttpClient owner, Guid workspaceId, string email)
    {
        var before = factory.Recorded.MemberInvitationsTo(email).Count;
        var response = await owner.PostAsJsonAsync(InvitationsUrl(workspaceId), new { email });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(before + 1, factory.Recorded.MemberInvitationsTo(email).Count);

        return (await response.Content.ReadFromJsonAsync<MemberInvitationResponse>())!;
    }

    private async Task<(HttpClient Http, Guid UserId, string Email)> JoinAs(HttpClient owner, Guid workspaceId)
    {
        var email = NewEmail();
        await Invite(owner, workspaceId, email);
        var http = factory.CreateClient();
        var registered = await http.PostAsJsonAsync($"/api/invitations/{TokenFor(email)}/register", new { fullName = "Test Member", password = Password });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        var me = await http.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        return (http, me!.Id, email);
    }

    private async Task<(HttpClient Http, Guid WorkspaceId)> Owner()
    {
        var (http, _, _) = await SignedInWithEmail();
        var response = await http.PostAsJsonAsync("/api/workspaces", new { name = "Studio" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (http, (await response.Content.ReadFromJsonAsync<WorkspaceResponse>())!.Id);
    }

    private async Task<(HttpClient Http, Guid UserId, string Email)> SignedInWithEmail()
    {
        var http = factory.CreateClient();
        var email = NewEmail();
        var response = await http.PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = "Test User" });
        response.EnsureSuccessStatusCode();

        return (http, (await response.Content.ReadFromJsonAsync<CurrentUserResponse>())!.Id, email);
    }

    private async Task<(HttpClient Http, Guid UserId)> SignedIn()
    {
        var (http, userId, _) = await SignedInWithEmail();

        return (http, userId);
    }
}
