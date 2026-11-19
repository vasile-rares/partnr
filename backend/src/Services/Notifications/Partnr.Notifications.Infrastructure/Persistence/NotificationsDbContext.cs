using Microsoft.EntityFrameworkCore;
using Npgsql;
using Partnr.Notifications.Application.Exceptions;
using Partnr.Notifications.Domain.Entities;

namespace Partnr.Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    private const string UniqueViolationSqlState = "23505";

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<WithdrawnRequest> WithdrawnRequests => Set<WithdrawnRequest>();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState })
        {
            // Two deliveries of the same event at once: the one that lost finds the notification already there when it is tried again.
            throw new ConflictException("A record with the same unique value already exists.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
}
