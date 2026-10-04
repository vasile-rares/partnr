namespace ClientPortal.Application.Common;

public interface IUnitOfWork
{
    /// <exception cref="Exceptions.ConflictException">A unique constraint was violated.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
