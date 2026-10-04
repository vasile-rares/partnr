using ClientPortal.SharedKernel.Primitives;

namespace ClientPortal.Domain.Events;

public sealed record ClientRegisteredEvent(Guid ClientId) : DomainEvent;
