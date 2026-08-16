using System.Diagnostics;
using System.Runtime.Versioning;

namespace LibTmux.Examples;

/// <summary>Runs every example against a tmux server of its own.</summary>
/// <remarks>
/// Every example is written the way a caller would write it, and each runs
/// here so that an example which stops compiling, or stops working, is a
/// build failure rather than something a reader discovers.
///
/// This is the same list <c>LibTmux.ExampleTests</c> runs, discovered the same
/// way. Running them here keeps the loop a caller can use — one command, real
/// output on the console — and running them there gives each one a name in a
/// test report.
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
