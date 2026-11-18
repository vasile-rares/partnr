using Partnr.Workspaces.Application.DTOs.ProjectUpdates;
using Partnr.Workspaces.Domain.Entities;

namespace Partnr.Workspaces.Application.Mappings;

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
