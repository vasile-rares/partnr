using ClientPortal.Application.DTOs.Deliverables;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Mappings;

/// <summary>A deliverable's response also carries its approval rounds, so building one needs the repository that holds them.</summary>
internal sealed class DeliverableResponseBuilder(IApprovalRequestRepository approvals)
{
    public async Task<DeliverableResponse> BuildAsync(Deliverable deliverable, Project project, CancellationToken cancellationToken) =>
        deliverable.ToResponse(
            project,
            await approvals.ListForDeliverableAsync(deliverable.WorkspaceId, deliverable.ProjectId, deliverable.Id, cancellationToken));

    public async Task<IReadOnlyList<DeliverableResponse>> BuildAsync(
        IReadOnlyList<Deliverable> deliverables,
        Project project,
        CancellationToken cancellationToken) =>
        deliverables.ToResponses(project, await approvals.ListForProjectAsync(project.WorkspaceId, project.Id, cancellationToken));
}
