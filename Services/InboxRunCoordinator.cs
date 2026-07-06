using Microsoft.Extensions.Logging;

namespace CheapClerk.Services;

public sealed class InboxRunCoordinator(
    Func<CancellationToken, Task<InboxProcessingReport>> inboxRunner,
    ILogger<InboxRunCoordinator> logger)
{
    private readonly Lock _sync = new();
    private bool _running;
    private bool _pendingRun;

    public void RequestRun()
    {
        lock (_sync)
        {
            if (_running)
            {
                _pendingRun = true;
                return;
            }
            _running = true;
        }

        _ = Task.Run(RunUntilDrainedAsync);
    }

    private async Task RunUntilDrainedAsync()
    {
        while (true)
        {
            try
            {
                var report = await inboxRunner(CancellationToken.None);
                if (report.SkippedReason is not null)
                    logger.LogInformation("Triggered inbox run skipped: {Reason}", report.SkippedReason);
                else if (report.InboxCount > 0)
                    logger.LogInformation(
                        "Triggered inbox run: {Found} found, {Applied} applied, {Review} to review, {Failed} failed",
                        report.InboxCount,
                        report.Outcomes.Count(o => o.Applied),
                        report.Outcomes.Count(o => o.SentToReview),
                        report.Outcomes.Count(o => o.Error is not null));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Triggered inbox run failed");
            }

            lock (_sync)
            {
                if (!_pendingRun)
                {
                    _running = false;
                    return;
                }
                _pendingRun = false;
            }
        }
    }
}
