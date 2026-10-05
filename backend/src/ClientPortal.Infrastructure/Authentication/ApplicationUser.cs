using Microsoft.AspNetCore.Identity;

namespace ClientPortal.Infrastructure.Authentication;

/// <summary>An account that can sign in. Roles are not global: they will belong to workspace memberships.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    public string FullName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
