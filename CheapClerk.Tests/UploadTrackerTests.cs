using System.Net;
using System.Text;
using CheapClerk.Configuration;
using CheapClerk.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CheapClerk.Tests;

public sealed class UploadTrackerTests
{
    private static PaperlessClient BuildPaperlessClient(StubHttpHandler stub) =>
        new(
            new HttpClient(stub) { BaseAddress = new Uri("http://paperless.test/") },
            Options.Create(new PaperlessOptions()),
            NullLogger<PaperlessClient>.Instance);

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    // Wraps delay-call counting so the count can be inspected after the async call completes.
    private sealed class DelayCallCounter
    {
        public int Calls { get; private set; }

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task UploadAndTrackAsync_SuccessOnSecondPoll_ReturnsConsumedWithParsedDocumentId()
    {
        var pollCount = 0;
        var stub = new StubHttpHandler(outgoing =>
        {
            if (outgoing.RequestUri!.AbsolutePath.EndsWith("post_document/"))
                return Ok("\"task-uuid-1\"");

            pollCount++;
            return pollCount == 1
                ? Ok("[{\"task_id\":\"task-uuid-1\",\"status\":\"PENDING\",\"result\":null,\"related_document\":null}]")
                : Ok("[{\"task_id\":\"task-uuid-1\",\"status\":\"SUCCESS\",\"result\":\"ok\",\"related_document\":\"5\"}]");
        });
        var counter = new DelayCallCounter();
        var tracker = new UploadTracker(BuildPaperlessClient(stub), NullLogger<UploadTracker>.Instance, counter.Delay);

        var outcome = await tracker.UploadAndTrackAsync(
            "PDF content"u8.ToArray(), "factuur.pdf", TimeSpan.FromSeconds(30));

        Assert.Equal(UploadOutcomeKind.Consumed, outcome.Kind);
        Assert.Equal(5, outcome.DocumentId);
        Assert.Equal("ok", outcome.Detail);
        Assert.Equal(2, pollCount);
        Assert.Equal(2, counter.Calls);
    }

    [Fact]
    public async Task UploadAndTrackAsync_FailureStatus_ReturnsFailedWithVerbatimMessage()
    {
        const string failureMessage = "Not consuming duplicate.pdf: it is a duplicate";
        var stub = new StubHttpHandler(outgoing =>
        {
            if (outgoing.RequestUri!.AbsolutePath.EndsWith("post_document/"))
                return Ok("\"task-uuid-2\"");

            return Ok(
                $"[{{\"task_id\":\"task-uuid-2\",\"status\":\"FAILURE\",\"result\":\"{failureMessage}\",\"related_document\":null}}]");
        });
        var counter = new DelayCallCounter();
        var tracker = new UploadTracker(BuildPaperlessClient(stub), NullLogger<UploadTracker>.Instance, counter.Delay);

        var outcome = await tracker.UploadAndTrackAsync(
            "PDF content"u8.ToArray(), "duplicate.pdf", TimeSpan.FromSeconds(30));

        Assert.Equal(UploadOutcomeKind.Failed, outcome.Kind);
        Assert.Equal(failureMessage, outcome.Detail);
        Assert.Null(outcome.DocumentId);
    }

    [Fact]
    public async Task UploadAndTrackAsync_StillPendingAfterBudgetExhausted_ReturnsStillProcessingAtMaxPolls()
    {
        var stub = new StubHttpHandler(outgoing =>
        {
            if (outgoing.RequestUri!.AbsolutePath.EndsWith("post_document/"))
                return Ok("\"task-uuid-3\"");

            return Ok("[{\"task_id\":\"task-uuid-3\",\"status\":\"PENDING\",\"result\":null,\"related_document\":null}]");
        });
        var counter = new DelayCallCounter();
        var tracker = new UploadTracker(BuildPaperlessClient(stub), NullLogger<UploadTracker>.Instance, counter.Delay);

        // pollBudget = 10s -> max polls = 10/2 = 5 (integer division)
        var outcome = await tracker.UploadAndTrackAsync(
            "PDF content"u8.ToArray(), "slow.pdf", TimeSpan.FromSeconds(10));

        Assert.Equal(UploadOutcomeKind.StillProcessing, outcome.Kind);
        Assert.Equal("consumption is still running — the clerk will file it automatically", outcome.Detail);
        Assert.Equal(5, counter.Calls);
    }

    [Fact]
    public async Task UploadAndTrackAsync_UploadRejected_ReturnsRejectedWithZeroPolls()
    {
        var stub = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var counter = new DelayCallCounter();
        var tracker = new UploadTracker(BuildPaperlessClient(stub), NullLogger<UploadTracker>.Instance, counter.Delay);

        var outcome = await tracker.UploadAndTrackAsync(
            "PDF content"u8.ToArray(), "broken.pdf", TimeSpan.FromSeconds(30));

        Assert.Equal(UploadOutcomeKind.UploadRejected, outcome.Kind);
        Assert.Equal("upload was rejected by Paperless — check the logs", outcome.Detail);
        Assert.Equal(0, counter.Calls);
    }
}
