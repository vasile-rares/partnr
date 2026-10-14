using ClientPortal.Application.DTOs.Projects;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Mappings;

/// <summary>
/// Turns projects into what the workspace sees. The client's name goes through <see cref="ClientResponseBuilder"/>, so a client
/// with an account shows the account's name here too, never the card's old one.
/// </summary>
internal sealed class ProjectResponseBuilder(IClientRepository clients, ClientResponseBuilder clientResponses)
{
    public async Task<ProjectResponse> BuildAsync(Project project, CancellationToken cancellationToken) =>
        (await BuildAsync(project.WorkspaceId, [project], cancellationToken))[0];

    public async Task<IReadOnlyList<ProjectResponse>> BuildAsync(
        Guid workspaceId,
        IReadOnlyList<Project> projects,
        CancellationToken cancellationToken)
    {
        if (projects.Count == 0)
        {
            return [];
        }

        var cards = await clients.ListByIdsAsync(workspaceId, projects.Select(p => p.ClientId).Distinct().ToList(), cancellationToken);
        var names = (await clientResponses.BuildAsync(cards, cancellationToken))
            .ToDictionary(c => c.Id, c => c.Name ?? c.Email);

        return projects.Select(p => new ProjectResponse(
            p.Id,
            new ProjectClientResponse(p.ClientId, names.GetValueOrDefault(p.ClientId, "Unknown client")),
            p.Name,
            p.Description,
            p.StartDate,
            p.DueDate,
            p.Status.ToString(),
            p.CreatedAtUtc,
            p.ArchivedAtUtc)).ToList();
    }
}
