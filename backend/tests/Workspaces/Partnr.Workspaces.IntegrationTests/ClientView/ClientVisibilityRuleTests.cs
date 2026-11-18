using System.Net.Http.Json;
using Partnr.Workspaces.Application.DTOs.Auth;
using Partnr.Workspaces.Application.DTOs.Workspaces;
using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;
using Partnr.Workspaces.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Partnr.Workspaces.IntegrationTests.ClientView;

/// <summary>The client's SQL filter (ProjectRepository) and the domain rule (Project.IsOpenToClient) must give the same answer.</summary>
[Collection(ApiCollection.Name)]
public sealed class ClientVisibilityRuleTests(ApiFactory factory)
{
    public enum Card { Linked, ArchivedCard, NoAccount, SomeoneElse }

    [Theory]
    [InlineData(ProjectStatus.Active, false, Card.Linked)]
    [InlineData(ProjectStatus.OnHold, false, Card.Linked)]
    [InlineData(ProjectStatus.Completed, false, Card.Linked)]
    [InlineData(ProjectStatus.Planning, false, Card.Linked)]
    [InlineData(ProjectStatus.Active, true, Card.Linked)]
    [InlineData(ProjectStatus.Active, false, Card.ArchivedCard)]
    [InlineData(ProjectStatus.Active, false, Card.NoAccount)]
    [InlineData(ProjectStatus.Active, false, Card.SomeoneElse)]
    public async Task TheDatabaseAndTheDomainAgree(ProjectStatus status, bool archivedProject, Card card)
    {
        var owner = await Register();
        var clientAccount = await Register();
        var someoneElse = await Register();
        var workspaceId = await CreateWorkspace(owner.Http);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var client = Client.Register(workspaceId, "Elena", $"{Guid.NewGuid():N}@example.com");
        switch (card)
        {
            case Card.Linked:
            case Card.ArchivedCard:
                client.LinkTo(clientAccount.UserId, DateTime.UtcNow);
                break;
            case Card.SomeoneElse:
                client.LinkTo(someoneElse.UserId, DateTime.UtcNow);
                break;
        }

        if (card == Card.ArchivedCard)
        {
            client.Archive();
        }

        var project = Project.Create(workspaceId, client.Id, "Website");
        project.ChangeStatus(status);
        if (archivedProject)
        {
            project.Archive();
        }

        db.Clients.Add(client);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var inDatabase = await projects.GetSharedWithClientAsync(clientAccount.UserId, project.Id, CancellationToken.None) is not null;
        var inDomain = project.IsOpenToClient(client) && client.UserId == clientAccount.UserId;

        Assert.Equal(inDomain, inDatabase);
    }

    private async Task<(HttpClient Http, Guid UserId)> Register()
    {
        var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync(
            "/api/auth/register",
            new { email = $"user-{Guid.NewGuid():N}@example.com", password = "correct horse", fullName = "Test User" });
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();

        return (http, user!.Id);
    }

    private static async Task<Guid> CreateWorkspace(HttpClient http)
    {
        var response = await http.PostAsJsonAsync("/api/workspaces", new { name = "Studio" });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<WorkspaceResponse>())!.Id;
    }
}
