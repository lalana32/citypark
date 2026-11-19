namespace CityPark.Infrastructure.Tests.Concurrency;

public class ParallelRunnerTests
{
    [Fact]
    public async Task RunAsync_RunsAllCallsAtTheSameTime()
    {
        const int count = 20;
        var started = 0;
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Sequential calls would never see all of them started, so WaitAsync would fail.
        var results = await ParallelRunner.RunAsync(count, async _ =>
        {
            if (Interlocked.Increment(ref started) == count)
            {
                allStarted.SetResult();
            }

            await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        });

        Assert.Equal(count, results.Count);
        Assert.All(results, Assert.Null);
    }

    [Fact]
    public async Task RunAsync_ReturnsEachOutcomeAtItsIndex()
    {
        var results = await ParallelRunner.RunAsync(4, index =>
            index % 2 == 0
                ? Task.CompletedTask
                : throw new InvalidOperationException($"Call {index} failed."));

        Assert.Null(results[0]);
        Assert.Equal("Call 1 failed.", Assert.IsType<InvalidOperationException>(results[1]).Message);
        Assert.Null(results[2]);
        Assert.Equal("Call 3 failed.", Assert.IsType<InvalidOperationException>(results[3]).Message);
    }

    [Fact]
    public async Task RunAsync_WithoutCalls_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ParallelRunner.RunAsync(0, _ => Task.CompletedTask));
    }
}
