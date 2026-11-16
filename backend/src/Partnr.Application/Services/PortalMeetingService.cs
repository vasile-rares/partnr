using Partnr.Application.Authorization;
using Partnr.Application.DTOs.Meetings;
using Partnr.Application.Exceptions;
using Partnr.Application.Interfaces.Repositories;
using Partnr.Application.Interfaces.Services;

namespace Partnr.Application.Services;

internal sealed class PortalMeetingService(
    IMeetingRepository meetings,
    IProjectRepository projects,
    IWorkspaceRepository workspaces,
    WorkspaceAccess access,
    TimeProvider time) : IPortalMeetingService
{
    /// <summary>Cancelled meetings stay visible for a while, so the client sees what was called off; older history is not the portal's business.</summary>
    private static readonly TimeSpan History = TimeSpan.FromDays(30);

    public async Task<IReadOnlyList<PortalMeetingResponse>> ListAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = access.RequireUserId();
        if (!(await workspaces.ListForClientAsync(userId, cancellationToken)).Any(w => w.Id == workspaceId))
        {
            throw new NotFoundException($"The workspace with id '{workspaceId}' was not found.");
        }

        var list = await meetings.ListForClientAccountAsync(workspaceId, userId, time.GetUtcNow().UtcDateTime - History, cancellationToken);
        // The same door as the portal's projects: a project in Planning or archived is not the client's to see, so it is not named.
        var open = (await projects.ListSharedWithClientAsync(userId, cancellationToken))
            .Where(p => p.WorkspaceId == workspaceId)
            .ToDictionary(p => p.Id, p => p.Name);

        return list.Select(m => new PortalMeetingResponse(
            m.Id,
            m.ProjectId is { } projectId && open.TryGetValue(projectId, out var name) ? new MeetingProjectResponse(projectId, name) : null,
            m.Title,
            m.Description,
            m.StartsAtUtc,
            m.EndsAtUtc,
            m.Status.ToString(),
            m.IsCancelled ? null : m.MeetUrl)).ToList();
    }
}
