using System.Runtime.CompilerServices;
using Partnr.Application.Interfaces.Services;
using Partnr.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Partnr.Infrastructure.Persistence;

/// <summary>
/// After a save that worked, tells the accounts whose inbox it touched: a notification added, read or made obsolete.
/// The email worker's own changes (status, attempts) are not an inbox change and push nothing.
/// </summary>
internal sealed class InboxPushInterceptor(IInboxPublisher publisher, ILogger<InboxPushInterceptor> logger) : SaveChangesInterceptor
{
    // The interceptor is shared by every context, so what a save touched is kept per context.
    private readonly ConditionalWeakTable<DbContext, HashSet<Guid>> _touched = new();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);

        return ValueTask.FromResult(result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && _touched.TryGetValue(context, out var touched) && touched.Count > 0)
        {
            var users = touched.ToArray();
            touched.Clear();

            try
            {
                await publisher.InboxChangedAsync(users, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The change is saved; a missed push is repaired by the next load.
                logger.LogWarning(ex, "Could not tell {Count} account(s) that their inbox changed.", users.Length);
            }
        }

        return result;
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && _touched.TryGetValue(context, out var touched))
        {
            touched.Clear();
        }

        return Task.CompletedTask;
    }

    private void Collect(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var touched = _touched.GetOrCreateValue(context);
        foreach (var entry in context.ChangeTracker.Entries<Notification>())
        {
            var changed = entry.State == EntityState.Added
                || (entry.State == EntityState.Modified
                    && (entry.Property(n => n.ReadAtUtc).IsModified || entry.Property(n => n.ObsoleteAtUtc).IsModified));
            if (changed)
            {
                touched.Add(entry.Entity.RecipientUserId);
            }
        }
    }
}
