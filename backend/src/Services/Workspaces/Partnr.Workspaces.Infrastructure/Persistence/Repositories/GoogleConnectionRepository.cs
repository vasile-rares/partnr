using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Partnr.Workspaces.Infrastructure.Persistence.Repositories;

internal sealed class GoogleConnectionRepository(ApplicationDbContext dbContext) : IGoogleConnectionRepository
{
    public Task<GoogleConnection?> GetForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.GoogleConnections.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public void Add(GoogleConnection connection) => dbContext.GoogleConnections.Add(connection);

    public void Remove(GoogleConnection connection) => dbContext.GoogleConnections.Remove(connection);
}
