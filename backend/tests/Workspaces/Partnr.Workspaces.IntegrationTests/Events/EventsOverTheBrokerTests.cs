using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Contracts.Events;
using Partnr.Workspaces.Application.DTOs.Clients;
using Partnr.Workspaces.Application.DTOs.Projects;
using Partnr.Workspaces.Application.DTOs.Workspaces;
using Partnr.Workspaces.Application.Exceptions;
using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Infrastructure.Persistence;

namespace Partnr.Workspaces.IntegrationTests.Events;

// The whole trip: a service publishes, the outbox keeps the event with the change, RabbitMQ carries it, a consumer's queue receives it.
public sealed class EventsOverTheBrokerTests(BrokerApiFactory factory) : IClassFixture<BrokerApiFactory>
{
    private static readonly TimeSpan Arrival = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task Creating_a_project_sends_ProjectCreated_to_a_consumer_over_the_broker()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var project = await CreateProject(http, workspaceId, "Website");

        var received = await Arrives(project.Id);

        Assert.Equal(workspaceId, received.WorkspaceId);
        Assert.Equal("Website", received.Name);
    }

    [Fact]
    public async Task The_handler_the_api_itself_ships_receives_the_event_too()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var project = await CreateProject(http, workspaceId, "Probe me");

        var deadline = DateTime.UtcNow + Arrival;
        while (!factory.LogLines.Any(line => line.StartsWith("Probe:", StringComparison.Ordinal) && line.Contains(project.Id.ToString())))
        {
            Assert.True(DateTime.UtcNow < deadline, "The probe handler of the API did not log the event: Wolverine does not discover the handlers of the Api assembly.");
            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task An_event_of_a_save_that_fails_is_never_sent()
    {
        var (http, workspaceId) = await SignedInWithWorkspace();
        var lost = new ProjectCreated(Guid.NewGuid(), workspaceId, Guid.NewGuid(), "Never saved", DateTime.UtcNow);
        var sameEmail = $"twice-{Guid.NewGuid():N}@example.com";

        // The event is published, then the save is refused (the same email twice in one workspace): the outbox row goes with the rollback.
        using (var scope = factory.Services.CreateScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await events.PublishAsync(lost);
            db.Clients.Add(Client.Register(workspaceId, "One", sameEmail));
            db.Clients.Add(Client.Register(workspaceId, "Two", sameEmail));

            await Assert.ThrowsAsync<ConflictException>(() => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
        }

        // A control that does go out, sent after: once it has arrived, the lost one would have too.
        var control = await CreateProject(http, workspaceId, "Control");
        await Arrives(control.Id);
        Assert.DoesNotContain(factory.Received.ProjectsCreated, e => e.ProjectId == lost.ProjectId);
    }

    private async Task<ProjectCreated> Arrives(Guid projectId)
    {
        var deadline = DateTime.UtcNow + Arrival;
        while (DateTime.UtcNow < deadline)
        {
            if (factory.Received.ProjectsCreated.FirstOrDefault(e => e.ProjectId == projectId) is { } found)
            {
                return found;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"ProjectCreated for {projectId} did not arrive within {Arrival.TotalSeconds} s.");
    }

    private static async Task<ProjectResponse> CreateProject(HttpClient http, Guid workspaceId, string name)
    {
        var clientResponse = await http.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/clients", new { name = "Acme", email = $"acme-{Guid.NewGuid():N}@example.com" });
        clientResponse.EnsureSuccessStatusCode();
        var client = (await clientResponse.Content.ReadFromJsonAsync<ClientResponse>())!;

        var response = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects", new { clientId = client.Id, name });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProjectResponse>())!;
    }

    private async Task<(HttpClient Http, Guid WorkspaceId)> SignedInWithWorkspace()
    {
        var http = factory.CreateClient();
        var registered = await http.PostAsJsonAsync(
            "/api/auth/register", new { email = $"user-{Guid.NewGuid():N}@example.com", password = "correct horse", fullName = "Test User" });
        registered.EnsureSuccessStatusCode();

        var response = await http.PostAsJsonAsync("/api/workspaces", new { name = "Studio" });
        response.EnsureSuccessStatusCode();

        return (http, (await response.Content.ReadFromJsonAsync<WorkspaceResponse>())!.Id);
    }
}
