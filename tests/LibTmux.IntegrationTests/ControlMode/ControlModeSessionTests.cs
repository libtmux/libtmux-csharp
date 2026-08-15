using System.Runtime.Versioning;
using LibTmux.IntegrationTests.Infrastructure;
using LibTmux.IntegrationTests.Transport;

namespace LibTmux.IntegrationTests.ControlMode;

[UnsupportedOSPlatform("windows")]
public sealed class ControlModeSessionTests
{
    [UnixFact]
    public async Task A_control_client_answers_commands_and_reports_what_it_saw()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;
        Server server = await ConnectAsync(raw, token);

        await using IControlModeSession control = await server.EnterControlModeAsync(
            cancellationToken: token);

        Assert.True(control.IsRunning);

        // Attaching answers itself before anyone asks. A reader that handed
        // that block to the first caller would answer every command with the
        // previous one's output, so the first command has to get its own.
        IReadOnlyList<string> panes = await control.SendAsync(
            "list-panes -F '#{pane_id}'",
            token);

        Assert.Equal(["%0"], panes);

        IReadOnlyList<string> sessions = await control.SendAsync("list-sessions", token);
        Assert.Single(sessions);
    }

    [UnixFact]
    public async Task A_pane_id_inside_a_block_is_data_rather_than_a_notification()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;
        Server server = await ConnectAsync(raw, token);
        await using IControlModeSession control = await server.EnterControlModeAsync(
            cancellationToken: token);

        // tmux marks notifications with a leading percent, and a pane id starts
        // with one too. Ending the block at the first such line would truncate
        // this answer and leave the rest to be read as notifications.
        IReadOnlyList<string> reported = await control.SendAsync(
            "display-message -p '#{pane_id}'",
            token);

        Assert.Equal(["%0"], reported);

        // The stream is still in step: a command after the ambiguous one is
        // answered with its own output rather than the leftovers.
        Assert.Equal(["ok"], await control.SendAsync("display-message -p 'ok'", token));
    }

    [UnixFact]
    public async Task A_failing_command_faults_only_its_own_caller()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;
        Server server = await ConnectAsync(raw, token);
        await using IControlModeSession control = await server.EnterControlModeAsync(
            cancellationToken: token);

        await Assert.ThrowsAsync<TmuxCommandException>(
            () => control.SendAsync("no-such-tmux-command", token));

        // The client survives a rejected command, so the session is still
        // usable rather than needing to be torn down and reopened.
        Assert.True(control.IsRunning);
        Assert.Equal(["still-here"], await control.SendAsync(
            "display-message -p 'still-here'",
            token));
    }

    [UnixFact]
    public async Task Pane_output_arrives_decoded()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;
        Server server = await ConnectAsync(raw, token);
        await using IControlModeSession control = await server.EnterControlModeAsync(
            cancellationToken: token);

        await control.SendAsync("send-keys -t %0 'echo libtmux-control-marker' Enter", token);

        // tmux escapes the payload the way it escapes an option value, so a
        // reader that passed it through would report literal backslash-零-one-五
        // where the program wrote a carriage return.
        string seen = string.Empty;
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        await foreach (TmuxEvent observed in control.Events.WithCancellation(timeout.Token))
        {
            if (observed is TmuxOutputEvent output)
            {
                seen += output.Data;
                if (seen.Contains("libtmux-control-marker", StringComparison.Ordinal))
                {
                    break;
                }
            }
        }

        Assert.Contains("libtmux-control-marker", seen, StringComparison.Ordinal);
        Assert.DoesNotContain("\\015", seen, StringComparison.Ordinal);
    }

    [UnixFact]
    public async Task The_event_stream_ends_with_an_exit()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;
        Server server = await ConnectAsync(raw, token);
        IControlModeSession control = await server.EnterControlModeAsync(cancellationToken: token);

        await control.SendAsync("kill-server", token);

        List<TmuxEvent> observed = [];
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await foreach (TmuxEvent item in control.Events.WithCancellation(timeout.Token))
        {
            observed.Add(item);
        }

        // The stream completes rather than hanging, and says why it stopped, so
        // a caller awaiting it is released instead of waiting for a client that
        // is gone.
        Assert.NotEmpty(observed);
        Assert.IsType<TmuxExitEvent>(observed[^1]);
        await control.DisposeAsync();
    }

    private static Task<Server> ConnectAsync(
        RawTmuxTestContext raw,
        CancellationToken token) =>
        Server.ConnectAsync(
            new ServerConnectionOptions(
                tmuxBinaryPath: raw.TmuxBinaryPath,
                socketPath: raw.SocketPath,
                configurationFile: "/dev/null"),
            token);
}
