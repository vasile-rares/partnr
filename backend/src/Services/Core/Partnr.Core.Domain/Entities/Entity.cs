namespace Partnr.Core.Domain.Entities;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}
