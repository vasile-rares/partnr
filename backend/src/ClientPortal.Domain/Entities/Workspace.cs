using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.Domain.Entities;

public sealed class Workspace : Entity
{
    public const int NameMaxLength = 100;

    private readonly List<WorkspaceMember> _members = [];

    private Workspace()
    {
    }

    private Workspace(string name, DateTime createdAtUtc)
    {
        Name = name;
        CreatedAtUtc = createdAtUtc;
    }

    public string Name { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<WorkspaceMember> Members => _members.AsReadOnly();

    public static Workspace Create(string name, Guid ownerUserId)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new DomainException("A workspace needs an owner.");
        }

        var now = DateTime.UtcNow;
        var workspace = new Workspace(ValidName(name), now);
        workspace._members.Add(new WorkspaceMember(workspace.Id, ownerUserId, WorkspaceRole.Owner, now));

        return workspace;
    }

    public void Rename(string name) => Name = ValidName(name);

    public void AddMember(Guid userId, WorkspaceRole role)
    {
        if (RoleOf(userId) is not null)
        {
            throw new DomainException("This user is already a member of the workspace.");
        }

        _members.Add(new WorkspaceMember(Id, userId, role, DateTime.UtcNow));
    }

    public WorkspaceRole? RoleOf(Guid userId) => _members.FirstOrDefault(m => m.UserId == userId)?.Role;

    private static string ValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Workspace name is required.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException($"Workspace name must be at most {NameMaxLength} characters.");
        }

        return trimmed;
    }
}
