using Partnr.Core.Application.DTOs.ClientView;
using Partnr.Core.Application.DTOs.Projects;
using Partnr.Core.Application.Interfaces.Repositories;
using Partnr.Core.Domain.Entities;

namespace Partnr.Core.Application.Mappings;

/// <summary>A project as a client sees it: only what was shared, so building one needs the repositories of everything that can be shared.</summary>
internal sealed class ClientProjectResponseBuilder(
    IDeliverableRepository deliverables,
    IProjectUpdateRepository projectUpdates,
    IProjectFileRepository projectFiles,
    IApprovalRequestRepository approvals)
{
    public async Task<IReadOnlyList<ClientProjectSummaryResponse>> SummariesAsync(
        IReadOnlyList<Project> list,
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        var firm = new ClientWorkspaceResponse(workspace.Id, workspace.Name);
        var waiting = await approvals.CountPendingForClientAsync(list.Select(p => p.Id).ToList(), cancellationToken);

        return list
            .Select(p => new ClientProjectSummaryResponse(
                p.Id,
                firm,
                p.Name,
                p.Status.ToString(),
                p.DueDate,
                Progress(p),
                waiting.GetValueOrDefault(p.Id)))
            .ToList();
    }

    public async Task<ClientProjectResponse> BuildAsync(Project project, Workspace workspace, CancellationToken cancellationToken)
    {
        var stages = project.Stages;
        var stageNames = stages.ToDictionary(s => s.Id, s => s.Name);
        var shared = await deliverables.ListSharedAsync(project.Id, cancellationToken);
        var sharedUpdates = await projectUpdates.ListSharedAsync(project.Id, cancellationToken);
        var sharedFiles = await projectFiles.ListSharedAsync(project.Id, cancellationToken);
        var rounds = (await approvals.ListForProjectAsync(project.WorkspaceId, project.Id, cancellationToken)).ToLookup(a => a.DeliverableId);
        var sharedDeliverables = shared
            .Select(d => d.ToClientResponse(d.StageId is { } stageId ? stageNames.GetValueOrDefault(stageId) : null, rounds[d.Id]))
            .ToList();

        return new ClientProjectResponse(
            project.Id,
            new ClientWorkspaceResponse(workspace.Id, workspace.Name),
            project.Name,
            project.Description,
            project.StartDate,
            project.DueDate,
            project.Status.ToString(),
            Progress(project),
            stages.Select(s => new StageResponse(s.Id, s.Name, s.Description, s.DueDate, s.Status.ToString())).ToList(),
            sharedDeliverables,
            // Share() always sets the date, so a shared update has one.
            sharedUpdates.Select(u => new ClientProjectUpdateResponse(u.Id, u.Title, u.Body, u.SharedAtUtc!.Value)).ToList(),
            sharedFiles.Select(f => f.ToClientResponse()).ToList(),
            sharedDeliverables.Count(d => d.Approval.Pending is not null));
    }

    private static ProjectProgressResponse Progress(Project project) => new(project.DoneStageCount, project.Stages.Count);
}
