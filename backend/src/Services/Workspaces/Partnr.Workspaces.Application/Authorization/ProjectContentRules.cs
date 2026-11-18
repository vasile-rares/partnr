using Partnr.Workspaces.Application.Exceptions;

namespace Partnr.Workspaces.Application.Authorization;

/// <summary>What deliverables, updates and files share: archived ones are read-only, and only a draft can be deleted.</summary>
internal static class ProjectContentRules
{
    public static void EnsureNotArchived(bool isArchived, string kind)
    {
        if (isArchived)
        {
            throw new ConflictException($"This {kind} is archived. Restore it before making changes.");
        }
    }

    public static void EnsureDeletable(bool isDraft)
    {
        if (!isDraft)
        {
            throw new ConflictException("Only a draft can be deleted. Move it back to Draft, or archive it.");
        }
    }
}
