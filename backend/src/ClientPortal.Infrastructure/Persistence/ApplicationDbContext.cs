using ClientPortal.Application.Common.Interfaces;
using ClientPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientPortal.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Client> Clients => Set<Client>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
}
