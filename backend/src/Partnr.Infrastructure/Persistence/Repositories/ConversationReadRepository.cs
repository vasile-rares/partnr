using Partnr.Application.Interfaces.Repositories;
using Partnr.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Partnr.Infrastructure.Persistence.Repositories;

internal sealed class ConversationReadRepository(ApplicationDbContext dbContext) : IConversationReadRepository
{
    public Task<ConversationRead?> GetAsync(Guid userId, Guid projectId, CancellationToken cancellationToken) =>
        dbContext.ConversationReads.FirstOrDefaultAsync(r => r.UserId == userId && r.ProjectId == projectId, cancellationToken);

    public void Add(ConversationRead read) => dbContext.ConversationReads.Add(read);
}
