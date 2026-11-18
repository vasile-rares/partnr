using Partnr.Workspaces.Application.DTOs.ClientView;
using Partnr.Workspaces.Application.DTOs.ProjectFiles;
using Partnr.Workspaces.Domain.Entities;

namespace Partnr.Workspaces.Application.Mappings;

internal static class ProjectFileMappings
{
    public static ProjectFileResponse ToResponse(this ProjectFile file) =>
        new(
            file.Id,
            file.ProjectId,
            file.Name,
            file.Description,
            file.FileName,
            file.ContentType,
            file.SizeBytes,
            file.Status.ToString(),
            file.CreatedAtUtc,
            file.ArchivedAtUtc);

    public static ClientProjectFileResponse ToClientResponse(this ProjectFile file) =>
        new(file.Id, file.Name, file.Description, file.FileName, file.SizeBytes);
}
