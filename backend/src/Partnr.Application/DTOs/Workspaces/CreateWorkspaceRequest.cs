using System.ComponentModel.DataAnnotations;
using Partnr.Domain.Entities;

namespace Partnr.Application.DTOs.Workspaces;

public sealed record CreateWorkspaceRequest
{
    [Required]
    [MaxLength(Workspace.NameMaxLength)]
    public string Name { get; init; } = string.Empty;
}
