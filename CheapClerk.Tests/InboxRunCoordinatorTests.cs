using CheapClerk.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CheapClerk.Tests;

public sealed class InboxRunCoordinatorTests
{
    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition())
        {
            Assert.True(Environment.TickCount64 < deadline, "condition not reached within timeout");
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task RequestRun_WhenIdle_RunsExactlyOnce()
    {
        var runCount = 0;
        var coordinator = new InboxRunCoordinator(
            _ => { Interlocked.Increment(ref runCount); return Task.FromResult(new InboxProcessingReport()); },
            NullLogger<InboxRunCoordinator>.Instance);

        coordinator.RequestRun();

        await WaitUntilAsync(() => Volatile.Read(ref runCount) == 1);
        await Task.Delay(100);
        Assert.Equal(1, Volatile.Read(ref runCount));
    }

    [Fact]
    public async Task RequestRun_DuringRun_CoalescesToSingleFollowUp()
    {
        var runCount = 0;
        var firstRunStarted = new TaskCompletionSource();
        var releaseFirstRun = new TaskCompletionSource();
        var coordinator = new InboxRunCoordinator(
            async _ =>
            {
                var thisRun = Interlocked.Increment(ref runCount);
                if (thisRun == 1)
                {
                    firstRunStarted.TrySetResult();
                    await releaseFirstRun.Task;
                }
                return new InboxProcessingReport();
            },
            NullLogger<InboxRunCoordinator>.Instance);

        coordinator.RequestRun();
        await firstRunStarted.Task;
        coordinator.RequestRun();   // three triggers while running...
        coordinator.RequestRun();
        coordinator.RequestRun();
        releaseFirstRun.SetResult();

        await WaitUntilAsync(() => Volatile.Read(ref runCount) == 2);   // ...collapse into ONE follow-up
        await Task.Delay(150);
        Assert.Equal(2, Volatile.Read(ref runCount));
    }

    [Fact]
    public async Task RequestRun_AfterRunnerThrows_StillHonorsPendingAndFutureRuns()
    {
        var runCount = 0;
        var coordinator = new InboxRunCoordinator(
            _ =>
            {
                var thisRun = Interlocked.Increment(ref runCount);
                return thisRun == 1
                    ? Task.FromException<InboxProcessingReport>(new InvalidOperationException("boom"))
                    : Task.FromResult(new InboxProcessingReport());
            },
            NullLogger<InboxRunCoordinator>.Instance);

        coordinator.RequestRun();
        await WaitUntilAsync(() => Volatile.Read(ref runCount) == 1);
        coordinator.RequestRun();
        await WaitUntilAsync(() => Volatile.Read(ref runCount) == 2);
    }
}
