using Partnr.Application.DTOs.Meetings;
using Partnr.Application.DTOs.Projects;
using Partnr.Application.Interfaces.Repositories;
using Partnr.Domain.Entities;

namespace Partnr.Application.Mappings;

/// <summary>Names the client through <see cref="ClientResponseBuilder"/>, so the account's name wins, and the project from its own repository.</summary>
internal sealed class MeetingResponseBuilder(IClientRepository clients, ClientResponseBuilder clientResponses, IProjectRepository projects)
{
    public async Task<MeetingResponse> BuildAsync(Meeting meeting, CancellationToken cancellationToken) =>
        (await BuildAsync(meeting.WorkspaceId, [meeting], cancellationToken))[0];

    public async Task<IReadOnlyList<MeetingResponse>> BuildAsync(Guid workspaceId, IReadOnlyList<Meeting> meetings, CancellationToken cancellationToken)
    {
        if (meetings.Count == 0)
        {
            return [];
        }

        var cards = await clients.ListByIdsAsync(workspaceId, meetings.Select(m => m.ClientId).Distinct().ToList(), cancellationToken);
        var names = (await clientResponses.BuildAsync(cards, cancellationToken)).ToDictionary(c => c.Id, c => c.Name ?? c.Email);
        var projectNames = meetings.Any(m => m.ProjectId is not null)
            ? (await projects.ListAsync(workspaceId, includeArchived: true, clientId: null, cancellationToken)).ToDictionary(p => p.Id, p => p.Name)
            : [];

        return meetings.Select(m => new MeetingResponse(
            m.Id,
            new ProjectClientResponse(m.ClientId, names.GetValueOrDefault(m.ClientId, "Unknown client")),
            m.ProjectId is { } projectId && projectNames.TryGetValue(projectId, out var name) ? new MeetingProjectResponse(projectId, name) : null,
            m.Title,
            m.Description,
            m.StartsAtUtc,
            m.EndsAtUtc,
            m.Status.ToString(),
            m.CreatedAtUtc,
            m.CancelledAtUtc,
            m.SyncStatus.ToString(),
            m.MeetUrl)).ToList();
    }
}
