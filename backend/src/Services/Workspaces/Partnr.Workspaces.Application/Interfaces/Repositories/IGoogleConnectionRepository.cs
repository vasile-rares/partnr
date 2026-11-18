using Partnr.Workspaces.Domain.Entities;

namespace Partnr.Workspaces.Application.Interfaces.Repositories;

public interface IGoogleConnectionRepository
{
    Task<GoogleConnection?> GetForUserAsync(Guid userId, CancellationToken cancellationToken);

    void Add(GoogleConnection connection);

    void Remove(GoogleConnection connection);
}
