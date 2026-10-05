using ClientPortal.Domain.Common;

namespace ClientPortal.Domain.Clients;

public sealed class Client : Entity
{
    private Client()
    {
    }

    private Client(Guid workspaceId, string name, string email, DateTime createdAtUtc)
    {
        WorkspaceId = workspaceId;
        Name = name;
        Email = email;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid WorkspaceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public static Client Register(Guid workspaceId, string name, string email)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new DomainException("A client belongs to a workspace.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Client name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Client email is required.");
        }

        return new Client(workspaceId, name.Trim(), NormalizeEmail(email), DateTime.UtcNow);
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
