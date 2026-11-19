using Partnr.Notifications.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Partnr.Notifications.Infrastructure.Notifications;

/// <summary>Looks for due notification emails every few seconds. The work itself is <see cref="INotificationEmailService"/>.</summary>
internal sealed class NotificationEmailWorker(
    IServiceScopeFactory scopes,
    IOptions<NotificationOptions> options,
    ILogger<NotificationEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.EmailWorkerEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.EmailPollSeconds));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var run = await scope.ServiceProvider.GetRequiredService<INotificationEmailService>().SendDueAsync(stoppingToken);
                if (run.Failed > 0)
                {
                    logger.LogWarning("{Failed} notification email(s) failed and will be retried; {Sent} sent.", run.Failed, run.Sent);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The loop has to survive a database that is briefly away.
                logger.LogError(ex, "Sending notification emails failed.");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
