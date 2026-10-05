using System.ComponentModel.DataAnnotations;
using ClientPortal.Domain.Workspaces;

namespace ClientPortal.Application.Workspaces.Dtos.Requests;

public sealed record CreateWorkspaceRequest
{
    [Required]
    [MaxLength(Workspace.NameMaxLength)]
    public string Name { get; init; } = string.Empty;
}
