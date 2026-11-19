using Partnr.Integrations.Application.Interfaces.Repositories;
using Partnr.Integrations.Infrastructure.Persistence;
using Wolverine.EntityFrameworkCore;
using Wolverine.Runtime;

namespace Partnr.Integrations.Infrastructure.Events;

/// <summary>
/// Saves the changes and, in the same database transaction, the events published meanwhile (Wolverine's outbox); once the
/// transaction commits they are sent to the broker. A plain <c>SaveChangesAsync</c> on the context would lose them.
/// </summary>
internal sealed class OutboxUnitOfWork(IDbContextOutbox<IntegrationsDbContext> outbox) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await outbox.SaveChangesAndFlushMessagesAsync(MultiFlushMode.AllowMultiples, cancellationToken);
}
