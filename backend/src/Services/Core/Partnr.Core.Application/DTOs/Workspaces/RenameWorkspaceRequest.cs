using System.ComponentModel.DataAnnotations;
using Partnr.Core.Domain.Entities;

namespace Partnr.Core.Application.DTOs.Workspaces;

public sealed record RenameWorkspaceRequest
{
    [Required]
    [MaxLength(Workspace.NameMaxLength)]
    public string Name { get; init; } = string.Empty;
}
