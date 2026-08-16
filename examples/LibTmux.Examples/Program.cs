using System.Diagnostics;
using System.Runtime.Versioning;

namespace LibTmux.Examples;

/// <summary>Runs every example against a tmux server of its own.</summary>
/// <remarks>
/// The same list <c>LibTmux.ExampleTests</c> runs, on the console instead of
/// in a test report.
/// </remarks>
[UnsupportedOSPlatform("windows")]
internal static class Program
{
    private static async Task<int> Main()
    {
        if (OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("tmux does not run on Windows.");
            return 1;
        }

        int failed = 0;
        foreach (ExampleCase example in ExampleCase.Discover())
        {
            Console.WriteLine();
            Console.WriteLine($"── {example.Topic}.{example.Id} — {example.Title}");
            long started = Stopwatch.GetTimestamp();
            try
            {
                await example.RunAsync();
                Console.WriteLine(
                    $"   ok ({Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms)");
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                failed++;
                Console.Error.WriteLine($"   failed: {failure.Message}");
            }
        }

        Console.WriteLine();
        return failed == 0 ? 0 : 1;
    }
}
