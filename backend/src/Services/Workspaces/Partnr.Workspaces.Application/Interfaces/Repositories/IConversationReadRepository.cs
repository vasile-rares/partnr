using Partnr.Workspaces.Domain.Entities;

namespace Partnr.Workspaces.Application.Interfaces.Repositories;

public interface IConversationReadRepository
{
    Task<ConversationRead?> GetAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);

    void Add(ConversationRead read);
}
