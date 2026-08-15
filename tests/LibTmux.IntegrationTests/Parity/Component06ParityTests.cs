using System.Runtime.Versioning;
using LibTmux.IntegrationTests.Infrastructure;
using LibTmux.IntegrationTests.Transport;

namespace LibTmux.IntegrationTests.Parity;

[UnsupportedOSPlatform("windows")]
public sealed class Component06ParityTests
{
    public static TheoryData<string> OwnedRows =>
    [
        "libtmux.pane:Pane.from_env",
        "libtmux.server:Server.from_env",
        "libtmux.session:Session.from_env",
        "libtmux.window:Window.from_env",
    ];

    [Theory(
        Skip = "Requires a Unix process environment.",
        SkipType = typeof(UnixTestEnvironment),
        SkipUnless = nameof(UnixTestEnvironment.IsUnix))]
    [MemberData(nameof(OwnedRows))]
    public async Task Owned_parity_row_resolves_from_the_exported_environment(
        string pythonSymbolId)
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        RawTmuxResult identity = await raw.ExecuteAsync(
            ["list-panes", "-a", "-F", "#{pane_id}\t#{session_id}\t#{window_id}\t#{pid}"],
            TestContext.Current.CancellationToken);
        string[] fields = identity.StandardOutputLines[0].Split('\t');
        Dictionary<string, string> exported = new(StringComparer.Ordinal)
        {
            // tmux exports this shape into every pane it spawns.
            ["TMUX"] = $"{raw.SocketPath},{fields[3]},{fields[1].TrimStart('$')}",
            ["TMUX_PANE"] = fields[0],
        };

        bool proved = pythonSymbolId switch
        {
            "libtmux.server:Server.from_env" =>
                Server.FromEnvironment(exported).ConnectionOptions.SocketPath == raw.SocketPath,
            "libtmux.pane:Pane.from_env" =>
                (await Pane.FromEnvironmentAsync(
                    exported,
                    TestContext.Current.CancellationToken)).Id.ToString() == fields[0],
            "libtmux.window:Window.from_env" =>
                (await Window.FromEnvironmentAsync(
                    exported,
                    TestContext.Current.CancellationToken)).Id.ToString() == fields[2],
            "libtmux.session:Session.from_env" =>
                (await Session.FromEnvironmentAsync(
                    exported,
                    TestContext.Current.CancellationToken)).Id.ToString() == fields[1],
            _ => false,
        };

        Assert.True(proved, $"Parity behavior was not proved for {pythonSymbolId}.");
    }
}
