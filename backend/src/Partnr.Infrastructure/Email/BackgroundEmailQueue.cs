using System.Threading.Channels;
using Partnr.Application.DTOs.Email;
using Partnr.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Partnr.Infrastructure.Email;

/// <summary>The queue and the loop that empties it, one email at a time, through the regular <see cref="IEmailSender"/>.</summary>
internal sealed class BackgroundEmailQueue(IServiceScopeFactory scopes, ILogger<BackgroundEmailQueue> logger)
    : BackgroundService, IBackgroundEmailQueue
{
    private readonly Channel<EmailMessage> _queue = Channel.CreateUnbounded<EmailMessage>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(EmailMessage message) => _queue.Writer.TryWrite(message);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IEmailSender>().SendAsync(message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Not retried: the person did not get the link and asks for another one.
                logger.LogError(ex, "Could not send the email \"{Subject}\".", message.Subject);
            }
        }
    }
}
