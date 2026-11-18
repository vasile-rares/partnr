using Partnr.Contracts.Events;

namespace Partnr.Workspaces.Api.Events;

// Temporary: shows in the log that an event made the whole trip (outbox, broker, queue). The first real consumer, in its own service, replaces it.
public static class ProjectCreatedProbeHandler
{
    public static void Handle(ProjectCreated @event, ILogger<ProjectCreated> logger) =>
        logger.LogInformation("Probe: project {ProjectId} '{Name}' was created in workspace {WorkspaceId}.", @event.ProjectId, @event.Name, @event.WorkspaceId);
}
