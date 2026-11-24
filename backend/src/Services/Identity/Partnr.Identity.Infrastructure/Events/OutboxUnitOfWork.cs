using Partnr.Identity.Application.Interfaces.Repositories;
using Partnr.Identity.Infrastructure.Persistence;
using Wolverine.EntityFrameworkCore;
using Wolverine.Runtime;

namespace Partnr.Identity.Infrastructure.Events;

/// <summary>
/// Saves the changes and, in the same database transaction, the events published meanwhile (Wolverine's outbox); once the
/// transaction commits they are sent to the broker. A plain <c>SaveChangesAsync</c> on the context would lose them.
/// </summary>
internal sealed class OutboxUnitOfWork(IDbContextOutbox<ApplicationDbContext> outbox) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        // A use case saves once, but a few save again later in the same scope (a retry loop, a worker pass).
        await outbox.SaveChangesAndFlushMessagesAsync(MultiFlushMode.AllowMultiples, cancellationToken);
}
