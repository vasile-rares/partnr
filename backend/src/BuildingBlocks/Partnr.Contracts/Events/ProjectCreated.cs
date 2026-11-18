namespace Partnr.Contracts.Events;

/// <summary>A member started a project for a client in a workspace.</summary>
public sealed record ProjectCreated(Guid ProjectId, Guid WorkspaceId, Guid ClientId, string Name, DateTime CreatedAtUtc);
