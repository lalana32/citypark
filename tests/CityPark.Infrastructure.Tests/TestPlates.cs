namespace CityPark.Infrastructure.Tests;

/// <summary>
/// A new valid plate per call. The plate constraint (R7) spans all lots and users, and tests in a
/// class share one database and the same December days, so a fixed plate would make them collide.
/// </summary>
internal static class TestPlates
{
    private static int _next;

    // The T…T series is never used as a literal in the tests.
    public static string Next()
    {
        var n = Interlocked.Increment(ref _next);
        return $"T{n / 1000 % 100:00}T{n % 1000:000}";
    }
}
