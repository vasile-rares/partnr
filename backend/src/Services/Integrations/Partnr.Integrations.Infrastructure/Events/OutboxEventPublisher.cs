using Partnr.Integrations.Application.Interfaces.Services;
using Partnr.Integrations.Infrastructure.Persistence;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Partnr.Integrations.Infrastructure.Events;

internal sealed class OutboxEventPublisher(IDbContextOutbox<IntegrationsDbContext> outbox) : IEventPublisher
{
    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull =>
        await outbox.PublishAsync(@event);
}
