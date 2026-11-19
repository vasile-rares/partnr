using Partnr.Storage.Application.Interfaces.Repositories;

namespace Partnr.Storage.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(StorageDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
