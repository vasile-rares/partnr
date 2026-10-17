using System.Net;
using System.Net.Http.Json;
using ClientPortal.Application.DTOs.Auth;
using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.DTOs.Workspaces;
using ClientPortal.Domain.Enums;
using ClientPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.IntegrationTests.Clients;

[Collection(ApiCollection.Name)]
public sealed class ClientsTests(ApiFactory factory)
{
    [Fact]
    public async Task Register_StoresTheOptionalDetails()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();

        var created = await Register(http, workspaceId, new { name = "Acme", email = NewEmail(), company = "Acme Group", phone = " ", notes = "VIP" });
        var fetched = await http.GetFromJsonAsync<ClientResponse>($"/api/workspaces/{workspaceId}/clients/{created.Id}");

        Assert.Equal("Acme Group", fetched!.Company);
        Assert.Null(fetched.Phone);
        Assert.Equal("VIP", fetched.Notes);
        Assert.Null(fetched.ArchivedAtUtc);
    }

    [Fact]
    public async Task List_IsOrderedByName_AndHidesArchivedClientsUnlessAskedFor()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var beta = await Register(http, workspaceId, new { name = "Beta", email = NewEmail() });
        var alpha = await Register(http, workspaceId, new { name = "Alpha", email = NewEmail() });
        await http.PostAsync($"/api/workspaces/{workspaceId}/clients/{beta.Id}/archive", null);

        var active = await http.GetFromJsonAsync<ClientResponse[]>($"/api/workspaces/{workspaceId}/clients");
        var all = await http.GetFromJsonAsync<ClientResponse[]>($"/api/workspaces/{workspaceId}/clients?includeArchived=true");

        Assert.Equal(alpha.Id, Assert.Single(active!).Id);
        Assert.Equal(["Alpha", "Beta"], all!.Select(c => c.Name));
    }

    [Fact]
    public async Task Update_ChangesTheDetails()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var acme = await Register(http, workspaceId, new { name = "Acme", email = NewEmail(), phone = "0700" });
        var newEmail = NewEmail();

        var response = await http.PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/clients/{acme.Id}",
            new { name = "Acme SRL", email = newEmail.ToUpperInvariant(), company = "Acme Group" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await http.GetFromJsonAsync<ClientResponse>($"/api/workspaces/{workspaceId}/clients/{acme.Id}");
        Assert.Equal("Acme SRL", fetched!.Name);
        Assert.Equal(newEmail, fetched.Email);
        Assert.Equal("Acme Group", fetched.Company);
        Assert.Null(fetched.Phone);
    }

    [Fact]
    public async Task Update_ToTheEmailOfAnotherClient_ReturnsConflict_EvenIfThatClientIsArchived()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var takenEmail = NewEmail();
        var old = await Register(http, workspaceId, new { name = "Old", email = takenEmail });
        await http.PostAsync($"/api/workspaces/{workspaceId}/clients/{old.Id}/archive", null);
        var acme = await Register(http, workspaceId, new { name = "Acme", email = NewEmail() });

        var response = await http.PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/clients/{acme.Id}",
            new { name = "Acme", email = takenEmail });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithInvalidEmail_ReturnsBadRequest()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var acme = await Register(http, workspaceId, new { name = "Acme", email = NewEmail() });

        var response = await http.PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/clients/{acme.Id}",
            new { name = "Acme", email = "not-an-email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveThenRestore_TogglesTheArchivedDate()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var acme = await Register(http, workspaceId, new { name = "Acme", email = NewEmail() });

        var archive = await http.PostAsync($"/api/workspaces/{workspaceId}/clients/{acme.Id}/archive", null);
        var archived = await archive.Content.ReadFromJsonAsync<ClientResponse>();
        var restore = await http.PostAsync($"/api/workspaces/{workspaceId}/clients/{acme.Id}/restore", null);
        var restored = await restore.Content.ReadFromJsonAsync<ClientResponse>();

        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        Assert.NotNull(archived!.ArchivedAtUtc);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        Assert.Null(restored!.ArchivedAtUtc);
    }

    [Fact]
    public async Task AnotherUser_GetsNotFound_OnListingEditingAndArchiving()
    {
        var (owner, workspaceId) = await SignedInWithWorkspace();
        var acme = await Register(owner, workspaceId, new { name = "Acme", email = NewEmail() });
        var (stranger, _) = await SignedInWithWorkspace();
        var clientUrl = $"/api/workspaces/{workspaceId}/clients/{acme.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/workspaces/{workspaceId}/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync(clientUrl, new { name = "Hijacked", email = NewEmail() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"{clientUrl}/archive", null)).StatusCode);

        var unchanged = await owner.GetFromJsonAsync<ClientResponse>(clientUrl);
        Assert.Equal("Acme", unchanged!.Name);
        Assert.Null(unchanged.ArchivedAtUtc);
    }

    [Fact]
    public async Task Client_OfAnotherWorkspace_CannotBeEditedThroughMine()
    {
        var (http, mine) = await SignedInWithWorkspace();
        var other = await CreateWorkspace(http, "Other");
        var theirs = await Register(http, other.Id, new { name = "Acme", email = NewEmail() });

        var update = await http.PutAsJsonAsync(
            $"/api/workspaces/{mine}/clients/{theirs.Id}",
            new { name = "Hijacked", email = NewEmail() });
        var archive = await http.PostAsync($"/api/workspaces/{mine}/clients/{theirs.Id}/archive", null);

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, archive.StatusCode);
    }

    [Fact]
    public async Task Member_CanAddAndEditClients_ButNotArchiveOrRestoreThem()
    {
        var (owner, workspaceId) = await SignedInWithWorkspace();
        var acme = await Register(owner, workspaceId, new { name = "Acme", email = NewEmail() });
        var old = await Register(owner, workspaceId, new { name = "Old", email = NewEmail() });
        await owner.PostAsync($"/api/workspaces/{workspaceId}/clients/{old.Id}/archive", null);
        var (member, memberId) = await SignedIn();
        await AddMember(workspaceId, memberId);

        var added = await member.PostAsJsonAsync($"/api/workspaces/{workspaceId}/clients", new { name = "By member", email = NewEmail() });
        var edited = await member.PutAsJsonAsync($"/api/workspaces/{workspaceId}/clients/{acme.Id}", new { name = "Acme SRL", email = NewEmail() });
        var archive = await member.PostAsync($"/api/workspaces/{workspaceId}/clients/{acme.Id}/archive", null);
        var restore = await member.PostAsync($"/api/workspaces/{workspaceId}/clients/{old.Id}/restore", null);

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, restore.StatusCode);

        var stillActive = await owner.GetFromJsonAsync<ClientResponse>($"/api/workspaces/{workspaceId}/clients/{acme.Id}");
        var stillArchived = await owner.GetFromJsonAsync<ClientResponse>($"/api/workspaces/{workspaceId}/clients/{old.Id}");
        Assert.Null(stillActive!.ArchivedAtUtc);
        Assert.NotNull(stillArchived!.ArchivedAtUtc);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<(HttpClient Http, Guid WorkspaceId)> SignedInWithWorkspace()
    {
        var (http, _) = await SignedIn();
        var workspace = await CreateWorkspace(http, "Studio");

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

    private static async Task<WorkspaceResponse> CreateWorkspace(HttpClient http, string name)
    {
        var response = await http.PostAsJsonAsync("/api/workspaces", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkspaceResponse>())!;
    }

    private static async Task<ClientResponse> Register(HttpClient http, Guid workspaceId, object request)
    {
        var response = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/clients", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClientResponse>())!;
    }
}
