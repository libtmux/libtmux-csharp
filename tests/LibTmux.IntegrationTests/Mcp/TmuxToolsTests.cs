using System.Runtime.Versioning;
using LibTmux.IntegrationTests.Transport;
using LibTmux.Mcp;
using LibTmux.Testing;
using ModelContextProtocol;

namespace LibTmux.IntegrationTests;

[UnsupportedOSPlatform("windows")]
public sealed class TmuxToolsTests
{
    [UnixFact]
    public async Task Reading_tools_report_what_tmux_holds()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TmuxTestFactory factory = new();
        TmuxTestOptions options = HarnessOptions();
        await using TemporaryHierarchyScope scope = await factory.CreateHierarchyAsync(
            options,
            token);

        using TmuxConnectionAccessor connection = new(options.ConnectionOptions);
        TmuxTools tools = new(connection);

        string listing = await tools.ListTmuxAsync(token);
        Assert.Contains(scope.Session.Name, listing, StringComparison.Ordinal);
        Assert.Contains(scope.Pane.Id.ToString(), listing, StringComparison.Ordinal);

        // An assistant given a pane that has gone should be told so plainly.
        McpException missing = await Assert.ThrowsAsync<McpException>(
            () => tools.CaptureTmuxPaneAsync("%999", cancellationToken: token));
        Assert.Contains("%999", missing.Message, StringComparison.Ordinal);
    }

    [UnixFact]
    public async Task Acting_tools_change_what_the_reading_tools_then_see()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TmuxTestFactory factory = new();
        TmuxTestOptions options = HarnessOptions();
        await using TemporaryHierarchyScope scope = await factory.CreateHierarchyAsync(
            options,
            token);

        using TmuxConnectionAccessor connection = new(options.ConnectionOptions);
        TmuxTools tools = new(connection);

        await tools.RunInTmuxPaneAsync(scope.Pane.Id.ToString(), "echo mcp-ran", token);
        string text = await TmuxWait.UntilAsync(
            cancellation => tools.CaptureTmuxPaneAsync(
                scope.Pane.Id.ToString(),
                cancellationToken: cancellation),
            captured => captured.Contains("mcp-ran", StringComparison.Ordinal),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(20),
            token);
        Assert.Contains("mcp-ran", text, StringComparison.Ordinal);

        // Creating shows up in the next listing, which is how an assistant
        // learns what its own action did.
        string created = await tools.CreateTmuxSessionAsync("mcp-made", cancellationToken: token);
        Assert.Contains("mcp-made", created, StringComparison.Ordinal);
        Assert.Contains("mcp-made", await tools.ListTmuxAsync(token), StringComparison.Ordinal);

        string window = await tools.CreateTmuxWindowAsync(
            scope.Session.Id.ToString(),
            "mcp-window",
            token);
        Assert.Contains("mcp-window", window, StringComparison.Ordinal);

        // A session identifier nobody holds is refused rather than guessed at.
        await Assert.ThrowsAsync<McpException>(
            () => tools.CreateTmuxWindowAsync("$999", "nowhere", token));
    }

    private static TmuxTestOptions HarnessOptions() =>
        new(new ServerConnectionOptions(
            tmuxBinaryPath: Environment.GetEnvironmentVariable("LIBTMUX_TMUX") ?? "tmux",
            socketName: $"ltm-{Guid.NewGuid():N}"[..20],
            configurationFile: "/dev/null"));
}
