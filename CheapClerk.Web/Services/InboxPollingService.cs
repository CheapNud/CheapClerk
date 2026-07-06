using CheapClerk.Configuration;
using CheapClerk.Services;
using Microsoft.Extensions.Options;

namespace CheapClerk.Web.Services;

public sealed class InboxPollingService(
    InboxProcessorService inboxProcessor,
    IOptions<ClassificationOptions> classificationOptions,
    ILogger<InboxPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollMinutes = classificationOptions.Value.PollIntervalMinutes;
        if (!classificationOptions.Value.Enabled || pollMinutes <= 0)
        {
            logger.LogInformation("Inbox polling disabled (Enabled={Enabled}, PollIntervalMinutes={Minutes})",
                classificationOptions.Value.Enabled, pollMinutes);
            return;
        }

        // Grace delay so startup isn't slowed and Paperless is reachable
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        using var pollTimer = new PeriodicTimer(TimeSpan.FromMinutes(pollMinutes));
        do
        {
            try
            {
                var report = await inboxProcessor.ProcessInboxAsync(stoppingToken);
                if (report.InboxCount > 0)
                    logger.LogInformation("Inbox poll processed {Count} documents", report.InboxCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Inbox poll failed");
            }
        } while (await pollTimer.WaitForNextTickAsync(stoppingToken));
    }
}
