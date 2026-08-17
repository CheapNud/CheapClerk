using Microsoft.Extensions.Logging;

namespace CheapClerk.Services;

public enum UploadOutcomeKind { Consumed, Failed, StillProcessing, UploadRejected }

public sealed class UploadOutcome
{
    public UploadOutcomeKind Kind { get; set; }
    public string? Detail { get; set; }
    public int? DocumentId { get; set; }
}

public sealed class UploadTracker(
    PaperlessClient paperlessClient,
    ILogger<UploadTracker> logger,
    Func<TimeSpan, CancellationToken, Task>? delayOverride = null)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly Func<TimeSpan, CancellationToken, Task> _delay =
        delayOverride ?? ((delay, cancellationToken) => Task.Delay(delay, cancellationToken));

    public async Task<UploadOutcome> UploadAndTrackAsync(
        byte[] fileBytes, string fileName, TimeSpan pollBudget, CancellationToken cancellationToken = default)
    {
        var attempt = await paperlessClient.UploadDocumentAsync(fileBytes, fileName, cancellationToken);
        if (attempt.TaskUuid is null)
        {
            logger.LogWarning("Upload failed for {FileName}: {Detail}", fileName, attempt.FailureDetail);
            return new UploadOutcome
            {
                Kind = UploadOutcomeKind.UploadRejected,
                Detail = attempt.FailureDetail ?? "upload failed — check the logs"
            };
        }
        var taskUuid = attempt.TaskUuid;

        var maxPolls = Math.Max(1, (int)(pollBudget.TotalSeconds / 2));

        for (var poll = 0; poll < maxPolls; poll++)
        {
            await _delay(PollInterval, cancellationToken);

            var taskStatus = await paperlessClient.GetTaskStatusAsync(taskUuid, cancellationToken);
            if (taskStatus is null)
                continue;

            // Paperless 3.x lowercased task statuses ("success"); older versions
            // shout them ("SUCCESS") — match case-insensitively forever
            switch (taskStatus.Status?.ToUpperInvariant())
            {
                case "SUCCESS":
                    return new UploadOutcome
                    {
                        Kind = UploadOutcomeKind.Consumed,
                        DocumentId = int.TryParse(taskStatus.RelatedDocument, out var documentId) ? documentId : null,
                        Detail = taskStatus.Result
                    };
                case "FAILURE":
                    logger.LogWarning("Consumption failed for {FileName}: {Detail}", fileName, taskStatus.Result);
                    return new UploadOutcome
                    {
                        Kind = UploadOutcomeKind.Failed,
                        Detail = taskStatus.Result
                    };
            }
        }

        return new UploadOutcome
        {
            Kind = UploadOutcomeKind.StillProcessing,
            Detail = "consumption is still running — the clerk will file it automatically"
        };
    }
}
