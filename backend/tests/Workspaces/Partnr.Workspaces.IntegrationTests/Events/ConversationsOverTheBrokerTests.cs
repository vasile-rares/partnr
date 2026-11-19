using System.Net.Http.Json;
using Partnr.Contracts.Events;
using Partnr.Workspaces.Application.DTOs.Auth;
using Partnr.Workspaces.Application.DTOs.Clients;
using Partnr.Workspaces.Application.DTOs.Projects;
using Partnr.Workspaces.Application.DTOs.Workspaces;

namespace Partnr.Workspaces.IntegrationTests.Events;

// The live push of a conversation is the Notifications service's now; this service only says who has to hear about a change.
public sealed class ConversationsOverTheBrokerTests(BrokerApiFactory factory) : IClassFixture<BrokerApiFactory>
{
    [Fact]
    public async Task AMessage_SaysConversationChanged_ToEveryoneWhoReadsIt()
    {
        var http = factory.CreateClient();
        (await http.PostAsJsonAsync("/api/auth/register", new { email = $"user-{Guid.NewGuid():N}@example.com", password = "correct horse", fullName = "Test User" })).EnsureSuccessStatusCode();
        var me = (await http.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me"))!;
        var workspace = await Created<WorkspaceResponse>(await http.PostAsJsonAsync("/api/workspaces", new { name = "Studio" }));
        var clientEmail = $"elena-{Guid.NewGuid():N}@example.com";
        var client = await Created<ClientResponse>(await http.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/clients", new { name = "Elena", email = clientEmail }));

        // A conversation needs a client with an account: the invitation is how they get one.
        (await http.PostAsync($"/api/workspaces/{workspace.Id}/clients/{client.Id}/invitation", null)).EnsureSuccessStatusCode();
        var token = factory.Emails.InvitationsTo(clientEmail)[^1].Token;
        var clientHttp = factory.CreateClient();
        (await clientHttp.PostAsJsonAsync($"/api/invitations/{token}/register", new { fullName = "Elena", password = "correct horse" })).EnsureSuccessStatusCode();
        var elena = (await clientHttp.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me"))!;

        var project = await Created<ProjectResponse>(await http.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/projects", new { clientId = client.Id, name = "Website" }));

        (await http.PutAsJsonAsync($"/api/workspaces/{workspace.Id}/projects/{project.Id}", new { name = project.Name, description = project.Description, status = "Active" })).EnsureSuccessStatusCode();

        (await http.PostAsJsonAsync($"/api/workspaces/{workspace.Id}/conversations/{project.Id}/messages", new { body = "Hello" })).EnsureSuccessStatusCode();

        var changed = await Eventually(() => factory.Received.ConversationsChanged.FirstOrDefault(e => e.ProjectId == project.Id));
        Assert.Contains(me.Id, changed.UserIds);
        Assert.Contains(elena.Id, changed.UserIds);
    }

    private static async Task<T> Created<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<T> Eventually<T>(Func<T?> find)
        where T : class
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (find() is { } found)
            {
                return found;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("The event did not arrive within 20 s.");
    }
}
