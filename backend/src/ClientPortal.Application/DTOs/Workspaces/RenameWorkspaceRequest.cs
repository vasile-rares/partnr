using System.ComponentModel.DataAnnotations;
using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.DTOs.Workspaces;

public sealed record RenameWorkspaceRequest
{
    [Required]
    [MaxLength(Workspace.NameMaxLength)]
    public string Name { get; init; } = string.Empty;
}
