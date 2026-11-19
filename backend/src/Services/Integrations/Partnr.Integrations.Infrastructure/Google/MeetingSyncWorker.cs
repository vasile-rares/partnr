using Partnr.Integrations.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Partnr.Integrations.Infrastructure.Google;

/// <summary>Looks for meetings that are due to be sent to Google every few seconds. The work itself is <see cref="IMeetingSyncService"/>.</summary>
internal sealed class MeetingSyncWorker(
    IServiceScopeFactory scopes,
    IOptions<GoogleOptions> options,
    ILogger<MeetingSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        // Without Google credentials nobody can have a connection, so there is nothing to look for.
        if (!settings.SyncWorkerEnabled || !settings.IsConfigured)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.SyncPollSeconds));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var run = await scope.ServiceProvider.GetRequiredService<IMeetingSyncService>().SyncDueAsync(stoppingToken);
                if (run.Failed > 0 || run.NeedReconnect > 0)
                {
                    logger.LogWarning(
                        "Meeting sync: {Synced} done, {Failed} failed and will be retried, {NeedReconnect} wait for a Google reconnect.",
                        run.Synced,
                        run.Failed,
                        run.NeedReconnect);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The loop has to survive a database that is briefly away.
                logger.LogError(ex, "Syncing meetings with Google failed.");
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
