namespace Partnr.Contracts.Events;

/// <summary>A project file was deleted. Storage removes its bytes, and keeps trying until it works, so nothing is left orphaned.</summary>
public sealed record ProjectFileRemoved(string StorageKey);
