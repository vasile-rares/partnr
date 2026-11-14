using Partnr.Domain.Entities;

namespace Partnr.Application.Interfaces.Repositories;

public interface IConversationReadRepository
{
    Task<ConversationRead?> GetAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    void Add(ConversationRead read);
}
