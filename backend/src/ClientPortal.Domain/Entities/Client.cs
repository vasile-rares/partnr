using ClientPortal.Domain.Events;
using ClientPortal.SharedKernel.Primitives;

namespace ClientPortal.Domain.Entities;

public sealed class Client : Entity
{
    private Client()
    {
    }

    private Client(Guid id, string name, string email, DateTime createdAtUtc)
        : base(id)
    {
        Name = name;
        Email = email;
        CreatedAtUtc = createdAtUtc;
    }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public static Client Register(string name, string email)
    {
        var client = new Client(Guid.NewGuid(), name.Trim(), email.Trim().ToLowerInvariant(), DateTime.UtcNow);
        client.Raise(new ClientRegisteredEvent(client.Id));
        return client;
    }
}
