namespace ClientPortal.SharedKernel.Primitives;

public abstract class Entity
{
    private readonly List<DomainEvent> _domainEvents = [];

    protected Entity()
    {
    }

    protected Entity(Guid id) => Id = id;

    public Guid Id { get; protected set; }

    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
