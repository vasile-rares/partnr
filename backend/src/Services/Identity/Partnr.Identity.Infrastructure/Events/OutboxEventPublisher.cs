using Partnr.Identity.Application.Interfaces.Services;
using Partnr.Identity.Infrastructure.Persistence;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Partnr.Identity.Infrastructure.Events;

internal sealed class OutboxEventPublisher(IDbContextOutbox<ApplicationDbContext> outbox) : IEventPublisher
{
    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull =>
        await outbox.PublishAsync(@event);
}
