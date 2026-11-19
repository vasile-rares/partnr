namespace Partnr.Contracts.Events;

/// <summary>An account connected (or connected again) its Google Calendar: what waited for the permission goes back in the queue.</summary>
public sealed record GoogleConnected(Guid UserId);

/// <summary>An account removed its Google connection: what was still waiting to be sent now waits for a new one.</summary>
public sealed record GoogleDisconnected(Guid UserId);
