using System.Runtime.Versioning;
using LibTmux.Testing;

namespace LibTmux.Examples;

/// <summary>Runs each example against a tmux server of its own.</summary>
/// <remarks>
/// Every example is written the way a caller would write it, and each runs
/// here so that an example which stops compiling, or stops working, is a
/// build failure rather than something a reader discovers.
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
        await using TemporaryHierarchyScope scope = await factory.CreateHierarchyAsync();

        await ShowHierarchyAsync(scope);
        await RunACommandAsync(scope.Pane);
        await ReadAndWriteOptionsAsync(scope.Window);
        await ReactToAnEventAsync(scope.Server);
        await FilterWhatIsThereAsync(scope.Session);
        return 0;
    }

    private static async Task ShowHierarchyAsync(TemporaryHierarchyScope scope)
    {
        // A server holds sessions, a session holds windows, a window holds
        // panes. Each accessor answers a list rather than something that
        // reaches tmux again while it is being read.
        // A handle says what it read. The scope's server has not read tmux
        // yet, so what it can say is where it is, not what it found there.
        Console.WriteLine($"socket           {scope.Server.ConnectionOptions.SocketName}");
        Console.WriteLine($"session          {scope.Session.Name} ({scope.Session.Id})");
        foreach (Window window in await scope.Session.GetWindowsAsync())
        {
            Console.WriteLine($"  window {window.Index,-3} {window.Name}");
            foreach (Pane pane in await window.GetPanesAsync())
            {
                Console.WriteLine($"    pane {pane.Index,-3} {pane.Width}x{pane.Height}");
            }
        }
    }

    private static async Task RunACommandAsync(Pane pane)
    {
        await pane.SendTextAsync("echo the-pane-ran-this");
        await pane.EnterAsync();

        // tmux answers a command once it has accepted it, not once the shell
        // has finished, so the result is waited for rather than assumed.
        string text = await TmuxWait.UntilAsync(
            async token => string.Join('\n', await pane.CaptureAsync(cancellationToken: token)),
            captured => captured.Contains("the-pane-ran-this", StringComparison.Ordinal),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(20));
        Console.WriteLine($"captured         {text.Contains("the-pane-ran-this", StringComparison.Ordinal)}");
    }

    private static async Task ReadAndWriteOptionsAsync(Window window)
    {
        await window.Options.SetAsync(new SetOptionRequest("automatic-rename", "off"));
        TmuxOption option = (await window.Options.GetAsync(
            new GetOptionRequest("automatic-rename")))[0];

        // tmux has no types, so a value carries what tmux said alongside the
        // readings that text supports.
        Console.WriteLine($"automatic-rename {option.Value.Raw} (flag {option.Value.Boolean})");

        // An option the window does not hold is inherited rather than missing,
        // and asking for inherited values is what shows it.
        IReadOnlyList<TmuxOption> inherited = await window.Options.GetAsync(
            new GetOptionRequest("mode-keys", includeInherited: true));
        Console.WriteLine($"mode-keys        {inherited[0].Value.Raw} (inherited)");
    }

    private static async Task ReactToAnEventAsync(Server server)
    {
        // A hook is a tmux command tmux runs for itself when something
        // happens. Every hook is an array, even with one entry.
        TmuxHook hook = await server.Hooks.SetAsync(
            new SetHookRequest("alert-bell", "set-option -g @rang yes"));
        Console.WriteLine($"alert-bell       {hook.Values[0].Command}");

        await server.Hooks.RunAsync(new HookRequest("alert-bell"));
        IReadOnlyList<TmuxOption> rang = await server.Options.GetAsync(
            new GetOptionRequest("@rang", OptionScope.Session, global: true, quiet: true));
        Console.WriteLine($"hook ran         {rang.Count == 1}");
    }

    private static async Task FilterWhatIsThereAsync(Session session)
    {
        await session.CreateWindowAsync(new NewWindowRequest(name: "build-one"));
        await session.CreateWindowAsync(new NewWindowRequest(name: "build-two"));

        // Ordinary filtering is LINQ over what was read.
        IReadOnlyList<Window> windows = await session.GetWindowsAsync();
        int building = windows.Count(window =>
            window.Name.StartsWith("build", StringComparison.Ordinal));
        Console.WriteLine($"building         {building}");
    }
}
