using System.Runtime.Versioning;
using LibTmux.Testing;

namespace LibTmux.PackageConsumer;

/// <summary>Uses the library the way a downstream project would.</summary>
/// <remarks>
/// This reaches the library through the built package rather than through a
/// project reference, so it fails when the package is missing an assembly,
/// targets the wrong frameworks, or hides something a caller needs. None of
/// that is visible from inside the repository.
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

        TmuxTestFactory factory = new();
        TmuxTestOptions options = new(new ServerConnectionOptions(
            tmuxBinaryPath: Environment.GetEnvironmentVariable("LIBTMUX_TMUX") ?? "tmux",
            socketName: $"libtmux-pkg-{Guid.NewGuid():N}"[..24],
            configurationFile: "/dev/null"));

        await using TemporaryHierarchyScope scope = await factory.CreateHierarchyAsync(options);

        await scope.Pane.SendTextAsync("echo consumed-from-the-package");
        await scope.Pane.EnterAsync();
        string text = await TmuxWait.UntilAsync(
            async token => string.Join(
                '\n',
                await scope.Pane.CaptureAsync(cancellationToken: token)),
            captured => captured.Contains("consumed-from-the-package", StringComparison.Ordinal),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(20));

        Console.WriteLine($"session  {scope.Session.Name}");
        Console.WriteLine($"captured {text.Contains("consumed-from-the-package", StringComparison.Ordinal)}");
        return 0;
    }
}
