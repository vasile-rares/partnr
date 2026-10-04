using ClientPortal.Application.Common;
using ClientPortal.Application.Common.Exceptions;
using ClientPortal.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClientPortal.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IUnitOfWork
{
    private const string UniqueViolationSqlState = "23505";

    public DbSet<Client> Clients => Set<Client>();

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState })
        {
            // Also covers the race between an existence check and the insert: the unique index is the last line of defense.
            throw new ConflictException("A record with the same unique value already exists.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
}
