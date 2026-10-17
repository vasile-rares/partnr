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
public sealed class ClientSuggestionsTests(ApiFactory factory)
{
    private const string Password = "correct horse";

    [Fact]
    public async Task AClientCanBeAddedWithJustAnEmail_AndTheListShowsNoName()
    {
        var (studio, workspaceId) = await SignedInWithWorkspace();
        var email = NewEmail("Only.Email");

        var response = await studio.PostAsJsonAsync($"/api/workspaces/{workspaceId}/clients", new { email });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ClientResponse>();
        Assert.Null(created!.Name);
        Assert.Equal(email.ToLowerInvariant(), created.Email);

        var list = await studio.GetFromJsonAsync<ClientResponse[]>($"/api/workspaces/{workspaceId}/clients");
        Assert.Equal(created.Id, Assert.Single(list!).Id);
    }

    [Fact]
    public async Task AClientsNameCanBeCleared()
    {
        var (studio, workspaceId) = await SignedInWithWorkspace();
        var created = await (await studio.PostAsJsonAsync($"/api/workspaces/{workspaceId}/clients", new { name = "Acme", email = NewEmail() }))
            .Content.ReadFromJsonAsync<ClientResponse>();

        var updated = await studio.PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/clients/{created!.Id}",
            new { name = "", email = created.Email });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Null((await updated.Content.ReadFromJsonAsync<ClientResponse>())!.Name);
    }

    [Fact]
    public async Task Suggestions_FindAccountsByTheStartOfTheEmail_WithNames()
    {
        var (studio, workspaceId) = await SignedInWithWorkspace();
        var tag = Guid.NewGuid().ToString("N")[..10];
        await RegisterAccount($"zed.{tag}.one@example.com", "Zed One");
        await RegisterAccount($"zed.{tag}.two@example.com", "Zed Two");
        await RegisterAccount($"other.{tag}@example.com", "Someone Else");

        var found = await Suggest(studio, workspaceId, $"ZED.{tag}");

        Assert.Equal([$"zed.{tag}.one@example.com", $"zed.{tag}.two@example.com"], found.Select(f => f.Email));
        Assert.Equal(["Zed One", "Zed Two"], found.Select(f => f.FullName));
        Assert.All(found, f => Assert.False(f.AlreadyClient));
    }

    [Fact]
    public async Task Suggestions_FlagTheAccountsThatAreAlreadyClients_AndSkipTheWorkspaceMembers()
    {
        var (studio, workspaceId) = await SignedInWithWorkspace();
        var tag = Guid.NewGuid().ToString("N")[..10];
        var client = $"zed.{tag}.client@example.com";
        await RegisterAccount(client, "Zed Client");
        var member = await RegisterAccount($"zed.{tag}.member@example.com", "Zed Member");
        await AddMember(workspaceId, member.Id);
        await studio.PostAsJsonAsync($"/api/workspaces/{workspaceId}/clients", new { email = client });

        var found = await Suggest(studio, workspaceId, $"zed.{tag}");

        var only = Assert.Single(found);
        Assert.Equal(client, only.Email);
        Assert.True(only.AlreadyClient);
    }

    [Fact]
    public async Task Suggestions_AreLimitedToFiveResults_AndNeedAtLeastThreeCharacters()
    {
        var (studio, workspaceId) = await SignedInWithWorkspace();
        var tag = Guid.NewGuid().ToString("N")[..10];
        for (var i = 0; i < 7; i++)
        {
            await RegisterAccount($"many.{tag}.{i}@example.com", $"Many {i}");
        }

        Assert.Equal(5, (await Suggest(studio, workspaceId, $"many.{tag}")).Count);
        Assert.Empty(await Suggest(studio, workspaceId, "ma"));
        Assert.Empty(await Suggest(studio, workspaceId, "  "));
    }

    [Fact]
    public async Task Suggestions_TreatPercentAndUnderscoreAsPlainCharacters()
    {
        var (studio, workspaceId) = await SignedInWithWorkspace();
        var tag = Guid.NewGuid().ToString("N")[..10];
        await RegisterAccount($"wild.{tag}@example.com", "Wild");

        Assert.Empty(await Suggest(studio, workspaceId, "%%%"));
        Assert.Empty(await Suggest(studio, workspaceId, $"wil_.{tag}"));
        Assert.Single(await Suggest(studio, workspaceId, $"wild.{tag}"));
    }

    [Fact]
    public async Task Suggestions_AreNotFoundForSomeoneWhoIsNotAMember()
    {
        var (_, workspaceId) = await SignedInWithWorkspace();
        var (stranger, _) = await SignedIn();

        var response = await stranger.GetAsync($"/api/workspaces/{workspaceId}/clients/suggestions?email=abc");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Suggestions_RequireSigningIn()
    {
        var response = await factory.CreateClient().GetAsync($"/api/workspaces/{Guid.NewGuid()}/clients/suggestions?email=abc");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Suggestions_AreRateLimitedPerUser()
    {
        var (busy, busyWorkspace) = await SignedInWithWorkspace();
        var (calm, calmWorkspace) = await SignedInWithWorkspace();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 70; i++)
        {
            statuses.Add((await busy.GetAsync($"/api/workspaces/{busyWorkspace}/clients/suggestions?email=abc")).StatusCode);
        }

        Assert.Equal(60, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(10, statuses.Count(s => s == HttpStatusCode.TooManyRequests));
        Assert.Equal(HttpStatusCode.OK, (await calm.GetAsync($"/api/workspaces/{calmWorkspace}/clients/suggestions?email=abc")).StatusCode);
    }

    private static async Task<List<ClientSuggestionResponse>> Suggest(HttpClient http, Guid workspaceId, string typed)
    {
        var response = await http.GetAsync($"/api/workspaces/{workspaceId}/clients/suggestions?email={Uri.EscapeDataString(typed)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<List<ClientSuggestionResponse>>())!;
    }

    private static string NewEmail(string name = "user") => $"{name}-{Guid.NewGuid():N}@example.com";

    private async Task<(HttpClient Http, Guid UserId)> SignedIn()
    {
        var http = factory.CreateClient();
        var user = await RegisterWith(http, NewEmail(), "Test User");

        return (http, user.Id);
    }

    private async Task<(HttpClient Http, Guid WorkspaceId)> SignedInWithWorkspace()
    {
        var (http, _) = await SignedIn();
        var response = await http.PostAsJsonAsync("/api/workspaces", new { name = "Studio" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (http, (await response.Content.ReadFromJsonAsync<WorkspaceResponse>())!.Id);
    }

    // A separate client (cookie jar) so registering the account does not sign the test's own user out.
    private Task<CurrentUserResponse> RegisterAccount(string email, string fullName) =>
        RegisterWith(factory.CreateClient(), email, fullName);

    private static async Task<CurrentUserResponse> RegisterWith(HttpClient http, string email, string fullName)
    {
        var response = await http.PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CurrentUserResponse>())!;
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
}
