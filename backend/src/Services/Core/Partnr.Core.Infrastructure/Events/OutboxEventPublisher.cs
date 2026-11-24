using Partnr.Core.Application.Interfaces.Services;
using Partnr.Core.Infrastructure.Persistence;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Partnr.Core.Infrastructure.Events;

internal sealed class OutboxEventPublisher(IDbContextOutbox<ApplicationDbContext> outbox) : IEventPublisher
{
    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull =>
        await outbox.PublishAsync(@event);
}
