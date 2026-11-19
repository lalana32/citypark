namespace CityPark.Infrastructure.Tests.Concurrency;

public static class ParallelRunner
{
    /// <summary>
    /// Releases <paramref name="count"/> calls together and returns each outcome in index order:
    /// <c>null</c> on success, otherwise the thrown exception.
    /// </summary>
    public static async Task<IReadOnlyList<Exception?>> RunAsync(int count, Func<int, Task> call)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentNullException.ThrowIfNull(call);

        var arrived = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, count)
            .Select(index => Task.Run(async () =>
            {
                // The last call to arrive opens the gate for everyone.
                if (Interlocked.Increment(ref arrived) == count)
                {
                    gate.SetResult();
                }

                await gate.Task;
                await call(index);
            }))
            .ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        return [.. tasks.Select(task => task.Exception?.InnerException)];
    }
}
