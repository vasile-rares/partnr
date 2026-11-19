using System.Net;
using System.Net.Http.Json;
using Partnr.Workspaces.Application.DTOs.Messages;

namespace Partnr.Workspaces.IntegrationTests.ClientView;

// The project conversations, from both sides, through the same routes (/api/workspaces/...): a member of the firm and the client.
public sealed partial class ClientProjectsTests
{
    [Fact]
    public async Task TheFirmAndTheClient_WriteInTheSameConversation_AndSeeWhoWroteWhat()
    {
        var (studio, elena, project) = await OpenConversation("Elena");

        var first = await Send(studio.Http, FirmConversation(studio, project), "  Hi Elena,\nthe mockups are up.  ");
        var reply = await Send(elena.Http, ClientConversation(studio, project), "Thanks, looking now.");

        var asFirm = (await studio.Http.GetFromJsonAsync<ConversationResponse>(FirmConversation(studio, project)))!;
        var asClient = (await elena.Http.GetFromJsonAsync<ConversationResponse>(ClientConversation(studio, project)))!;

        Assert.Equal([first.Id, reply.Id], asFirm.Messages.Select(m => m.Id));
        Assert.Equal("Hi Elena,\nthe mockups are up.", asFirm.Messages[0].Body);
        Assert.Equal(("Owner", true, false), (asFirm.Messages[0].AuthorName, asFirm.Messages[0].IsMine, asFirm.Messages[0].FromClient));
        Assert.Equal(("Elena", false, true), (asFirm.Messages[1].AuthorName, asFirm.Messages[1].IsMine, asFirm.Messages[1].FromClient));
        Assert.Equal("Elena", asFirm.ClientName);
        Assert.Equal(("Owner", false), (asClient.Messages[0].AuthorName, asClient.Messages[0].IsMine));
        Assert.True(asClient.Messages[1].IsMine);
        Assert.Null(asClient.ClientName);
    }

    [Fact]
    public async Task UnreadCounts_AreEachPersons_AndReadingMovesTheMark()
    {
        var (studio, elena, project) = await OpenConversation("Elena");
        await Send(studio.Http, FirmConversation(studio, project), "One");
        var second = await Send(studio.Http, FirmConversation(studio, project), "Two");

        Assert.Equal(2, (await elena.Http.GetFromJsonAsync<UnreadMessagesResponse>(ClientUnread(studio)))!.Unread);
        Assert.Equal(0, (await studio.Http.GetFromJsonAsync<UnreadMessagesResponse>(FirmUnread(studio)))!.Unread); // their own messages

        var read = await elena.Http.PostAsJsonAsync($"{ClientConversation(studio, project)}/read", new { messageId = second.Id });
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        Assert.Equal(0, (await elena.Http.GetFromJsonAsync<UnreadMessagesResponse>(ClientUnread(studio)))!.Unread);

        await Send(elena.Http, ClientConversation(studio, project), "Got it");
        var list = (await studio.Http.GetFromJsonAsync<ConversationSummaryResponse[]>(FirmList(studio)))!;
        var summary = Assert.Single(list);
        Assert.Equal((1, "Elena", "Got it"), (summary.Unread, summary.LastMessage!.AuthorName, summary.LastMessage.Text));
    }

    [Fact]
    public async Task ReadingAnOlderMessage_NeverMakesNewerOnesReadOrUnreadAgain()
    {
        var (studio, elena, project) = await OpenConversation("Elena");
        var first = await Send(studio.Http, FirmConversation(studio, project), "One");
        var second = await Send(studio.Http, FirmConversation(studio, project), "Two");

        await elena.Http.PostAsJsonAsync($"{ClientConversation(studio, project)}/read", new { messageId = first.Id });
        Assert.Equal(1, (await elena.Http.GetFromJsonAsync<UnreadMessagesResponse>(ClientUnread(studio)))!.Unread);

        await elena.Http.PostAsJsonAsync($"{ClientConversation(studio, project)}/read", new { messageId = second.Id });
        await elena.Http.PostAsJsonAsync($"{ClientConversation(studio, project)}/read", new { messageId = first.Id }); // an old tab
        Assert.Equal(0, (await elena.Http.GetFromJsonAsync<UnreadMessagesResponse>(ClientUnread(studio)))!.Unread);
    }

    [Fact]
    public async Task TheAuthor_EditsOrDeletes_AndADeletedMessageKeepsItsPlaceWithoutItsWords()
    {
        var (studio, elena, project) = await OpenConversation("Elena");
        var url = FirmConversation(studio, project);
        var message = await Send(studio.Http, url, "Helo");
        Assert.NotNull(message.ChangeableUntilUtc);

        var edited = await studio.Http.PutAsJsonAsync($"{url}/messages/{message.Id}", new { body = "Hello" });
        var notMine = await elena.Http.PutAsJsonAsync($"{ClientConversation(studio, project)}/messages/{message.Id}", new { body = "Hacked" });
        var deleted = await studio.Http.DeleteAsync($"{url}/messages/{message.Id}");
        var again = await studio.Http.PutAsJsonAsync($"{url}/messages/{message.Id}", new { body = "Back" });

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.NotNull((await edited.Content.ReadFromJsonAsync<MessageResponse>())!.EditedAtUtc);
        Assert.Equal(HttpStatusCode.Forbidden, notMine.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var seen = (await elena.Http.GetFromJsonAsync<ConversationResponse>(ClientConversation(studio, project)))!.Messages.Single();
        Assert.Equal((true, (string?)null), (seen.IsDeleted, seen.Body));
        var preview = (await elena.Http.GetFromJsonAsync<ConversationSummaryResponse[]>(ClientList(studio)))!.Single().LastMessage!;
        Assert.Null(preview.Text);
        Assert.Equal(0, (await elena.Http.GetFromJsonAsync<UnreadMessagesResponse>(ClientUnread(studio)))!.Unread); // deleted ones are not unread
    }

    [Fact]
    public async Task AMessage_NeedsText_AndHasALimit()
    {
        var (studio, _, project) = await OpenConversation("Elena");
        var url = $"{FirmConversation(studio, project)}/messages";

        Assert.Equal(HttpStatusCode.BadRequest, (await studio.Http.PostAsJsonAsync(url, new { body = "   " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await studio.Http.PostAsJsonAsync(url, new { body = new string('x', 4001) })).StatusCode);
    }

    [Fact]
    public async Task AConversation_OnlyExists_WhileTheClientCanOpenTheProject()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var planning = await studio.AddProject(elena, "Still planning");

        Assert.Equal(HttpStatusCode.NotFound, (await studio.Http.GetAsync(FirmConversation(studio, planning))).StatusCode);
        Assert.Empty((await studio.Http.GetFromJsonAsync<ConversationSummaryResponse[]>(FirmList(studio)))!);

        await studio.SetStatus(planning, "Active");
        Assert.Equal(HttpStatusCode.OK, (await studio.Http.GetAsync(FirmConversation(studio, planning))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await studio.Http.PostAsync($"/api/workspaces/{studio.WorkspaceId}/projects/{planning.Id}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await studio.Http.GetAsync(FirmConversation(studio, planning))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await elena.Http.GetAsync(ClientConversation(studio, planning))).StatusCode);
    }

    [Fact]
    public async Task NobodyElse_ReachesAConversation()
    {
        var (studio, elena, project) = await OpenConversation("Elena");
        var (otherStudio, other, _) = await OpenConversation("Other");

        Assert.Equal(HttpStatusCode.NotFound, (await otherStudio.Http.GetAsync($"/api/workspaces/{otherStudio.WorkspaceId}/conversations/{project.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherStudio.Http.GetAsync(FirmConversation(studio, project))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.GetAsync(ClientConversation(studio, project))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await elena.Http.GetAsync($"/api/workspaces/{otherStudio.WorkspaceId}/conversations/{project.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.GetAsync(ClientList(studio))).StatusCode); // not their firm
    }

    [Fact]
    public async Task ALongConversation_ComesAPageAtATime_OldestFirstInEachPage()
    {
        var (studio, _, project) = await OpenConversation("Elena");
        var url = FirmConversation(studio, project);
        for (var i = 1; i <= 52; i++)
        {
            await Send(studio.Http, url, $"Message {i}");
        }

        var latest = (await studio.Http.GetFromJsonAsync<ConversationResponse>(url))!;
        var earlier = (await studio.Http.GetFromJsonAsync<ConversationResponse>($"{url}?before={latest.Messages[0].Id}"))!;

        Assert.Equal(50, latest.Messages.Count);
        Assert.True(latest.HasEarlier);
        Assert.Equal(("Message 3", "Message 52"), (latest.Messages[0].Body, latest.Messages[^1].Body));
        Assert.Equal(["Message 1", "Message 2"], earlier.Messages.Select(m => m.Body));
        Assert.False(earlier.HasEarlier);
    }

    private async Task<(StudioSession Studio, ClientSession Client, Partnr.Workspaces.Application.DTOs.Projects.ProjectResponse Project)> OpenConversation(string clientName)
    {
        var studio = await Studio();
        var client = await studio.AddClient(clientName);
        var project = await studio.AddProject(client, "Website");
        await studio.SetStatus(project, "Active");

        return (studio, client, project);
    }

    private static async Task<MessageResponse> Send(HttpClient http, string conversationUrl, string body)
    {
        var response = await http.PostAsJsonAsync($"{conversationUrl}/messages", new { body });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<MessageResponse>())!;
    }

    private static string FirmList(StudioSession studio) => $"/api/workspaces/{studio.WorkspaceId}/conversations";

    private static string FirmUnread(StudioSession studio) => $"{FirmList(studio)}/unread-count";

    private static string FirmConversation(StudioSession studio, Partnr.Workspaces.Application.DTOs.Projects.ProjectResponse project) =>
        $"{FirmList(studio)}/{project.Id}";

    private static string ClientList(StudioSession studio) => FirmList(studio); // one route for both sides

    private static string ClientUnread(StudioSession studio) => $"{ClientList(studio)}/unread-count";

    private static string ClientConversation(StudioSession studio, Partnr.Workspaces.Application.DTOs.Projects.ProjectResponse project) =>
        $"{ClientList(studio)}/{project.Id}";
}
