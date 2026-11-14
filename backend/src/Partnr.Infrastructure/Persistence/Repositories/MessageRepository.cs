using Partnr.Application.Interfaces.Repositories;
using Partnr.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Partnr.Infrastructure.Persistence.Repositories;

internal sealed class MessageRepository(ApplicationDbContext dbContext) : IMessageRepository
{
    public Task<Message?> GetByIdAsync(Guid projectId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Messages.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.Id == id, cancellationToken);

    public async Task<MessagePage> ListPageAsync(Guid projectId, Guid? beforeId, int limit, CancellationToken cancellationToken)
    {
        var query = dbContext.Messages.AsNoTracking().Where(m => m.ProjectId == projectId);

        if (beforeId is { } id)
        {
            var before = await dbContext.Messages.AsNoTracking()
                .Where(m => m.ProjectId == projectId && m.Id == id)
                .Select(m => new { m.CreatedAtUtc, m.Id })
                .FirstOrDefaultAsync(cancellationToken);
            if (before is null)
            {
                return new MessagePage([], false);
            }

            // Ids are UUID v7, so they break a tie in the same order the messages were written.
            query = query.Where(m => m.CreatedAtUtc < before.CreatedAtUtc || (m.CreatedAtUtc == before.CreatedAtUtc && m.Id < before.Id));
        }

        var newestFirst = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var page = newestFirst.Take(limit).Reverse().ToList();

        return new MessagePage(page, newestFirst.Count > limit);
    }

    public async Task<IReadOnlyDictionary<Guid, Message>> GetLatestPerProjectAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        var ids = projectIds.ToList();
        var latest = await dbContext.Messages
            .AsNoTracking()
            .Where(m => ids.Contains(m.ProjectId))
            .GroupBy(m => m.ProjectId)
            .Select(g => g.OrderByDescending(m => m.CreatedAtUtc).ThenByDescending(m => m.Id).First())
            .ToListAsync(cancellationToken);

        return latest.ToDictionary(m => m.ProjectId);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountUnreadPerProjectAsync(
        Guid userId,
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        var ids = projectIds.ToList();
        var counts = await (
            from message in dbContext.Messages
            where ids.Contains(message.ProjectId) && message.AuthorUserId != userId && message.DeletedAtUtc == null
            let readUpTo = dbContext.ConversationReads
                .Where(r => r.UserId == userId && r.ProjectId == message.ProjectId)
                .Select(r => (DateTime?)r.LastReadAtUtc)
                .FirstOrDefault()
            where readUpTo == null || message.CreatedAtUtc > readUpTo
            group message by message.ProjectId into perProject
            select new { ProjectId = perProject.Key, Count = perProject.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(c => c.ProjectId, c => c.Count);
    }

    public void Add(Message message) => dbContext.Messages.Add(message);
}
