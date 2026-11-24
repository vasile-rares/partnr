using Partnr.Core.Domain.Entities;

namespace Partnr.Core.Application.Interfaces.Repositories;

public interface IConversationReadRepository
{
    Task<ConversationRead?> GetAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    void Add(ConversationRead read);
}
