using ClientPortal.Application.DTOs.ProjectUpdates;
using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Mappings;

internal static class ProjectUpdateMappings
{
    public static ProjectUpdateResponse ToResponse(this ProjectUpdate update) =>
        new(
            update.Id,
            update.ProjectId,
            update.Title,
            update.Body,
            update.Status.ToString(),
            update.CreatedAtUtc,
            update.SharedAtUtc,
            update.ArchivedAtUtc);
}
