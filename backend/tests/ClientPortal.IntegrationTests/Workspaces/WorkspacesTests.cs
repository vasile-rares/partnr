using System.Net;
using System.Net.Http.Json;
using ClientPortal.Application.DTOs.Auth;
using ClientPortal.Application.DTOs.Workspaces;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using ClientPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.IntegrationTests.Workspaces;

[Collection(ApiCollection.Name)]
public sealed class WorkspacesTests(ApiFactory factory)
{
    [Fact]
    public async Task Create_MakesTheCreatorTheOwner_AndTheWorkspaceShowsUpEverywhere()
    {
        var (client, _) = await SignedInClient();

        var created = await CreateWorkspace(client, "  Studio Pop ");

        Assert.Equal("Studio Pop", created.Name);
        Assert.Equal("Owner", created.Role);

        var fetched = await client.GetFromJsonAsync<WorkspaceResponse>($"/api/workspaces/{created.Id}");
        Assert.Equal(created.Id, fetched!.Id);

        var mine = await client.GetFromJsonAsync<WorkspaceResponse[]>("/api/workspaces");
        Assert.Equal(created.Id, Assert.Single(mine!).Id);
    }

    [Fact]
    public async Task Create_WithoutName_ReturnsBadRequest()
    {
        var (client, _) = await SignedInClient();

        var response = await client.PostAsJsonAsync("/api/workspaces", new { name = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListMine_ShowsOnlyMyWorkspaces()
    {
        var (me, _) = await SignedInClient();
        var (someoneElse, _) = await SignedInClient();
        var mine = await CreateWorkspace(me, "Mine");
        await CreateWorkspace(someoneElse, "Theirs");

        var list = await me.GetFromJsonAsync<WorkspaceResponse[]>("/api/workspaces");

        Assert.Equal(mine.Id, Assert.Single(list!).Id);
    }

    [Fact]
    public async Task AnotherUser_GetsNotFound_OnTheWorkspaceItsClientsAndRenaming()
    {
        var (owner, _) = await SignedInClient();
        var workspace = await CreateWorkspace(owner, "Studio");
        var clientResponse = await owner.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/clients",
            new { name = "Acme", email = NewEmail() });
        var acme = await clientResponse.Content.ReadFromJsonAsync<ClientIdResponse>();
        var (stranger, _) = await SignedInClient();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/workspaces/{workspace.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/workspaces/{workspace.Id}/clients/{acme!.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await stranger.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/clients", new { name = "X", email = NewEmail() })).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await stranger.PatchAsJsonAsync($"/api/workspaces/{workspace.Id}", new { name = "Hijacked" })).StatusCode);

        // And the owner's data is untouched.
        var unchanged = await owner.GetFromJsonAsync<WorkspaceResponse>($"/api/workspaces/{workspace.Id}");
        Assert.Equal("Studio", unchanged!.Name);
    }

    [Fact]
    public async Task Rename_AsOwner_ChangesTheName()
    {
        var (owner, _) = await SignedInClient();
        var workspace = await CreateWorkspace(owner, "Studio");

        var response = await owner.PatchAsJsonAsync($"/api/workspaces/{workspace.Id}", new { name = "Studio Pop SRL" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await owner.GetFromJsonAsync<WorkspaceResponse>($"/api/workspaces/{workspace.Id}");
        Assert.Equal("Studio Pop SRL", fetched!.Name);
    }

    [Fact]
    public async Task Rename_AsMember_ReturnsForbidden_ButMemberCanStillReadAndAddClients()
    {
        var (owner, _) = await SignedInClient();
        var workspace = await CreateWorkspace(owner, "Studio");
        var (member, memberId) = await SignedInClient();
        await AddMember(workspace.Id, memberId);

        var rename = await member.PatchAsJsonAsync($"/api/workspaces/{workspace.Id}", new { name = "Hijacked" });
        var read = await member.GetFromJsonAsync<WorkspaceResponse>($"/api/workspaces/{workspace.Id}");
        var addClient = await member.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/clients",
            new { name = "Acme", email = NewEmail() });

        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);
        Assert.Equal("Member", read!.Role);
        Assert.Equal("Studio", read.Name);
        Assert.Equal(HttpStatusCode.Created, addClient.StatusCode);
    }

    [Fact]
    public async Task Clients_SameEmailInTwoWorkspacesIsAllowed_ButNotTwiceInOne()
    {
        var (client, _) = await SignedInClient();
        var first = await CreateWorkspace(client, "First");
        var second = await CreateWorkspace(client, "Second");
        var email = NewEmail();

        var inFirst = await client.PostAsJsonAsync($"/api/workspaces/{first.Id}/clients", new { name = "Acme", email });
        var inSecond = await client.PostAsJsonAsync($"/api/workspaces/{second.Id}/clients", new { name = "Acme", email });
        var duplicate = await client.PostAsJsonAsync($"/api/workspaces/{first.Id}/clients", new { name = "Acme again", email = email.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.Created, inFirst.StatusCode);
        Assert.Equal(HttpStatusCode.Created, inSecond.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Client_OfAnotherWorkspace_IsNotFound_EvenThroughMyOwnWorkspace()
    {
        var (client, _) = await SignedInClient();
        var first = await CreateWorkspace(client, "First");
        var second = await CreateWorkspace(client, "Second");
        var created = await client.PostAsJsonAsync($"/api/workspaces/{first.Id}/clients", new { name = "Acme", email = NewEmail() });
        var acme = await created.Content.ReadFromJsonAsync<ClientIdResponse>();

        var inRightWorkspace = await client.GetAsync($"/api/workspaces/{first.Id}/clients/{acme!.Id}");
        var inWrongWorkspace = await client.GetAsync($"/api/workspaces/{second.Id}/clients/{acme.Id}");

        Assert.Equal(HttpStatusCode.OK, inRightWorkspace.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inWrongWorkspace.StatusCode);
    }

    private sealed record ClientIdResponse(Guid Id);

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<(HttpClient Client, Guid UserId)> SignedInClient()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = NewEmail(), password = "correct horse", fullName = "Test User" });
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();

        return (client, user!.Id);
    }

    private static async Task<WorkspaceResponse> CreateWorkspace(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/workspaces", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkspaceResponse>())!;
    }

    // There is no endpoint for adding members yet (that comes with invitations), so tests add one straight to the database.
    private async Task AddMember(Guid workspaceId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var workspace = await db.Workspaces.FindAsync(workspaceId);
        await db.Entry(workspace!).Collection(w => w.Members).LoadAsync();
        workspace!.AddMember(userId, WorkspaceRole.Member);
        await db.SaveChangesAsync();
    }
}
