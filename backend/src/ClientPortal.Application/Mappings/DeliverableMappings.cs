using ClientPortal.Application.DTOs.Deliverables;
using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Mappings;

internal static class DeliverableMappings
{
    public static DeliverableResponse ToResponse(this Deliverable deliverable, Project project) =>
        new[] { deliverable }.ToResponses(project)[0];

    public static IReadOnlyList<DeliverableResponse> ToResponses(this IEnumerable<Deliverable> deliverables, Project project)
    {
        var stageNames = project.Stages.ToDictionary(s => s.Id, s => s.Name);

        return deliverables
            .Select(d => new DeliverableResponse(
                d.Id,
                d.ProjectId,
                d.StageId,
                d.StageId is { } stageId ? stageNames.GetValueOrDefault(stageId) : null,
                d.Title,
                d.Description,
                d.Url,
                d.Status.ToString(),
                d.CreatedAtUtc,
                d.ArchivedAtUtc))
            .ToList();
    }
}
