using Partnr.Integrations.Domain.Entities;

namespace Partnr.Integrations.Application.Interfaces.Repositories;

public interface IGoogleConnectionRepository
{
    Task<GoogleConnection?> GetForUserAsync(Guid userId, CancellationToken cancellationToken);

    void Add(GoogleConnection connection);

    void Remove(GoogleConnection connection);
}
