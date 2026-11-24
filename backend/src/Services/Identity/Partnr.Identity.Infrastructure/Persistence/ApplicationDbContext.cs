using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Partnr.Identity.Application.Exceptions;
using Partnr.Identity.Domain.Entities;
using Partnr.Identity.Infrastructure.Authentication;

namespace Partnr.Identity.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    private const string UniqueViolationSqlState = "23505";

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("This was changed a moment ago by someone else. Reload and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState })
        {
            // Also covers the race between an existence check and the insert: the unique index is the last line of defense.
            throw new ConflictException("A record with the same unique value already exists.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's mapping first, so our configurations can override it (table names, unique email index).
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Only renamed to match the snake_case table names; these Identity tables are not used yet.
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
    }
}
