using System.ComponentModel.DataAnnotations;

namespace Partnr.Notifications.Infrastructure.Notifications;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Off in the integration tests, which call the email service themselves so they stay deterministic.</summary>
    public bool EmailWorkerEnabled { get; init; } = true;

    [Range(1, 3600)]
    public int EmailPollSeconds { get; init; } = 5;
}
