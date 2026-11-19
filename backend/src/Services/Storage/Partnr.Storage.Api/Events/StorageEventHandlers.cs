using Partnr.Contracts.Events;
using Partnr.Storage.Application.Interfaces.Services;

namespace Partnr.Storage.Api.Events;

// What the other services say happened. Wolverine finds these by convention; delivery is at least once and deleting twice is harmless.
public static class ProjectFileRemovedHandler
{
    // A failure (the object store is down) escapes on purpose: Wolverine tries again, so no object is left behind.
    public static Task Handle(ProjectFileRemoved message, IFileStorageService storage, CancellationToken cancellationToken) =>
        storage.DeleteAsync(message.StorageKey, cancellationToken);
}
