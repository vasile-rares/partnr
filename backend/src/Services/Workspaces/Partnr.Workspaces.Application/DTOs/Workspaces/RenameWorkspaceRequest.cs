using System.ComponentModel.DataAnnotations;
using Partnr.Workspaces.Domain.Entities;

namespace Partnr.Workspaces.Application.DTOs.Workspaces;

public sealed record RenameWorkspaceRequest
{
    [Required]
    [MaxLength(Workspace.NameMaxLength)]
    public string Name { get; init; } = string.Empty;
}
