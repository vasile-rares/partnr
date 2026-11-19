using Partnr.Notifications.Application.Interfaces.Repositories;

namespace Partnr.Notifications.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(NotificationsDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
