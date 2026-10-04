namespace ClientPortal.SharedKernel.Primitives;

public abstract record DomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();

    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
