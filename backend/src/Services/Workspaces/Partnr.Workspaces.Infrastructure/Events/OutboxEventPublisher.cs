using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Infrastructure.Persistence;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Partnr.Workspaces.Infrastructure.Events;

internal sealed class OutboxEventPublisher(IDbContextOutbox<ApplicationDbContext> outbox) : IEventPublisher
{
    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull =>
        await outbox.PublishAsync(@event);
}
