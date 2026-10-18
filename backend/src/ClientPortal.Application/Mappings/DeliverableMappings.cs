using ClientPortal.Application.DTOs.Deliverables;
using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Mappings;

internal static class DeliverableMappings
{
    /// <param name="approvals">Rounds of this deliverable, newest first.</param>
    public static DeliverableResponse ToResponse(this Deliverable deliverable, Project project, IReadOnlyList<ApprovalRequest> approvals) =>
        new[] { deliverable }.ToResponses(project, approvals)[0];

    /// <param name="approvals">Rounds of any deliverable of the project, newest first.</param>
    public static IReadOnlyList<DeliverableResponse> ToResponses(
        this IEnumerable<Deliverable> deliverables,
        Project project,
        IReadOnlyList<ApprovalRequest> approvals)
    {
        var stageNames = project.Stages.ToDictionary(s => s.Id, s => s.Name);
        var rounds = approvals.ToLookup(a => a.DeliverableId);

        return deliverables
            .Select(d =>
            {
                var own = rounds[d.Id].ToList();

                return new DeliverableResponse(
                    d.Id,
                    d.ProjectId,
                    d.StageId,
                    d.StageId is { } stageId ? stageNames.GetValueOrDefault(stageId) : null,
                    d.Title,
                    d.Description,
                    d.Url,
                    d.Status.ToString(),
                    own.ShownStatus(),
                    own.Select(a => a.ToResponse()).ToList(),
                    d.CreatedAtUtc,
                    d.ArchivedAtUtc);
            })
            .ToList();
    }
}
