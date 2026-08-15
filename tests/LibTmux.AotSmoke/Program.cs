using System.Runtime.Versioning;
using LibTmux.Testing;

namespace LibTmux.AotSmoke;

/// <summary>Drives the library from an ahead-of-time published binary.</summary>
/// <remarks>
/// Trim and ahead-of-time warnings are only complete once something is
/// published that way, and a warning that reaches a caller is a failure they
/// see at run time rather than at build. This exercises the parts a caller
/// reaches without an expression tree, which is the surface that claims to be
/// safe to publish.
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

        // Connecting reads a running server's generation, so the server is
        // started rather than assumed. The scope kills it on the way out.
        TmuxTestFactory factory = new();
        TmuxTestOptions options = new(new ServerConnectionOptions(
            tmuxBinaryPath: Environment.GetEnvironmentVariable("LIBTMUX_TMUX") ?? "tmux",
            socketName: $"libtmux-aot-{Guid.NewGuid():N}"[..24],
            configurationFile: "/dev/null"));

        await using TemporaryHierarchyScope scope = await factory.CreateHierarchyAsync(options);
        {
            Server server = scope.Server;
            Session session = scope.Session;
            Window window = scope.Window;
            Pane pane = scope.Pane;

            await window.Options.SetAsync(new SetOptionRequest("automatic-rename", "off"));
            TmuxOption option = (await window.Options.GetAsync(
                new GetOptionRequest("automatic-rename")))[0];

            await server.SetBufferAsync("aot", "libtmux-aot");
            string buffer = await server.GetBufferAsync("libtmux-aot");

            Console.WriteLine($"session {session.Name}");
            Console.WriteLine($"pane    {pane.Width}x{pane.Height}");
            Console.WriteLine($"option  {option.Value.Raw}");
            Console.WriteLine($"buffer  {buffer}");
            return option.Value.Boolean == false && buffer == "aot" ? 0 : 1;
        }
    }
}
