using System.Runtime.Versioning;
using LibTmux.Testing;

namespace LibTmux.PackageConsumer;

/// <summary>Uses the library the way a downstream project would.</summary>
/// <remarks>
/// Reaches the library through the built package, not a project reference, to
/// catch a missing assembly, wrong target framework, or gap invisible from
/// inside the repository.
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
