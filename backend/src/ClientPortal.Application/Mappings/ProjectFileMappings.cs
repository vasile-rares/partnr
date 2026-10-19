using ClientPortal.Application.DTOs.Portal;
using ClientPortal.Application.DTOs.ProjectFiles;
using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Mappings;

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

    public static PortalProjectFileResponse ToPortalResponse(this ProjectFile file) =>
        new(file.Id, file.Name, file.Description, file.FileName, file.SizeBytes);
}
