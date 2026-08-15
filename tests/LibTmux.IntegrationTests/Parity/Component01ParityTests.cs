using System.Runtime.Versioning;
using LibTmux.IntegrationTests.Infrastructure;
using LibTmux.IntegrationTests.Transport;
using LibTmux.Internal;

namespace LibTmux.IntegrationTests.Parity;

[UnsupportedOSPlatform("windows")]
public sealed class Component01ParityTests
{
    public static TheoryData<string> OwnedRows =>
    [
        "libtmux.common:<module>",
        "libtmux.common:CmdMixin",
        "libtmux.common:CmdMixin.cmd",
        "libtmux.common:CmdProtocol",
        "libtmux.common:EnvironmentMixin.cmd",
        "libtmux.common:raise_if_stderr",
        "libtmux.common:tmux_cmd",
        "libtmux.pane:Pane.cmd",
        "libtmux.server:Server.cmd",
        "libtmux.session:Session.cmd",
        "libtmux.window:Window.cmd",
    ];

    [Theory(
        Skip = "Requires a Unix process environment.",
        SkipType = typeof(UnixTestEnvironment),
        SkipUnless = nameof(UnixTestEnvironment.IsUnix))]
    [MemberData(nameof(OwnedRows))]
    public async Task Owned_parity_row_dispatches_through_the_approved_boundary(
        string pythonSymbolId)
    {
        await using RawTmuxTestContext context = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        var transport = new TmuxProcessTransport(
            context.TmuxBinaryPath,
            context.BuildInvocationArguments([]));
        var dispatcher = new TmuxCommandDispatcher(transport);

        TmuxCommandResult result = pythonSymbolId switch
        {
            "libtmux.common:tmux_cmd" => await transport.ExecuteAsync(
                ["list-sessions", "-F", "#{session_name}"],
                TestContext.Current.CancellationToken),
            "libtmux.common:raise_if_stderr" => await ProveFailurePolicyAsync(transport),
            "libtmux.pane:Pane.cmd" => await new Pane(
                    dispatcher,
                    $"{context.SessionName}:0.0")
                .ExecuteCommandAsync(
                    ["display-message", "-p", "#{pane_id}"],
                    cancellationToken: TestContext.Current.CancellationToken),
            "libtmux.server:Server.cmd" => await new Server(dispatcher).ExecuteCommandAsync(
                ["display-message", "-p", "#{socket_path}"],
                TestContext.Current.CancellationToken),
            "libtmux.session:Session.cmd" => await new Session(dispatcher, context.SessionName)
                .ExecuteCommandAsync(
                    ["display-message", "-p", "#{session_name}"],
                    cancellationToken: TestContext.Current.CancellationToken),
            "libtmux.window:Window.cmd" => await new Window(
                    dispatcher,
                    $"{context.SessionName}:0")
                .ExecuteCommandAsync(
                    ["display-message", "-p", "#{window_id}"],
                    cancellationToken: TestContext.Current.CancellationToken),
            _ => await dispatcher.ExecuteAsync(
                ["list-sessions", "-F", "#{session_name}"],
                TestContext.Current.CancellationToken),
        };

        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.StandardOutputLines);
    }

    private static async Task<TmuxCommandResult> ProveFailurePolicyAsync(
        TmuxProcessTransport transport)
    {
        TmuxCommandResult failed = await transport.ExecuteAsync(
            ["definitely-not-a-libtmux-command"],
            TestContext.Current.CancellationToken);

        TmuxCommandException error = Assert.Throws<TmuxCommandException>(
            () => TmuxCommandFailure.ThrowIfFailed(failed, "display message"));
        Assert.Same(failed, error.Result);
        return new TmuxCommandResult(
            failed.Arguments,
            0,
            "failure-policy-proved\n"u8.ToArray(),
            failed.StandardError,
            ["failure-policy-proved"],
            failed.StandardErrorLines);
    }
}
