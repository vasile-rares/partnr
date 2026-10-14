using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.Projects;

public sealed record MoveStageRequest
{
    /// <summary>"up" swaps the stage with the one before it, "down" with the one after it. At either end nothing changes.</summary>
    [Required]
    [AllowedValues("up", "down")]
    public string Direction { get; init; } = string.Empty;
}
