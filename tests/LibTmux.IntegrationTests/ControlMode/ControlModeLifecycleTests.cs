using System.Diagnostics;
using System.Runtime.Versioning;
using LibTmux.IntegrationTests.Infrastructure;
using LibTmux.IntegrationTests.Transport;

namespace LibTmux.IntegrationTests.ControlMode;

/// <summary>Proves the control session survives the shapes that are not a happy command.</summary>
/// <remarks>
/// The other control-mode tests describe a session doing its job: commands
/// succeed, errors come back as errors, output decodes. These describe it under
/// the conditions that make a long-running client fail -- nobody reading events,
/// a caller cancelling mid-send, and disposal of a client that is still busy.
/// </remarks>
[UnsupportedOSPlatform("windows")]
public sealed class ControlModeLifecycleTests
{
    [UnixFact]
    public async Task Events_nobody_reads_do_not_wedge_the_reader()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;
        Server server = await ConnectAsync(raw, token);

        await using IControlModeSession control = await server.EnterControlModeAsync(
            cancellationToken: token);

        // Nothing reads Events for the whole test. Every one of these produces
        // notifications, and an unbounded channel would hold all of them. What
        // is being proved is not the buffering policy but its consequence: a
        // consumer that never arrives must not stop commands being answered,
        // because the same reader does both.
        for (int index = 0; index < 200; index++)
        {
            await control.SendAsync($"new-window -d -n w{index}", token);
        }

        IReadOnlyList<string> windows = await control.SendAsync(
            "list-windows -F '#{window_name}'",
            token);

        Assert.Contains("w199", windows);
    }

    [UnixFact]
    public async Task A_cancelled_send_does_not_misalign_later_replies()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;
        Server server = await ConnectAsync(raw, token);

        await using IControlModeSession control = await server.EnterControlModeAsync(
            cancellationToken: token);

        // A token already cancelled means the write never commits. A waiter
        // queued before the write would still be sitting in the queue, and tmux
        // answers in order, so it would take the next command's answer and every
        // later caller would be one reply behind.
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => control.SendAsync("display-message -p first", cancelled.Token));

        IReadOnlyList<string> reply = await control.SendAsync(
            "display-message -p second",
            token);

        Assert.Contains("second", string.Join('\n', reply), StringComparison.Ordinal);
    }

    [UnixFact]
    public async Task Disposal_returns_rather_than_waiting_forever()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;
        Server server = await ConnectAsync(raw, token);

        IControlModeSession control = await server.EnterControlModeAsync(cancellationToken: token);
        await control.SendAsync("display-message -p ready", token);

        // Disposal closes stdin and waits, then kills the client if the wait
        // runs out. Either path has to return: a disposal that hangs is one a
        // caller cannot recover from, and it is reached from a finally block.
        Stopwatch clock = Stopwatch.StartNew();
        await control.DisposeAsync();
        clock.Stop();

        Assert.True(
            clock.Elapsed < TimeSpan.FromSeconds(30),
            $"Disposal took {clock.Elapsed}, which is long enough to be unbounded.");
    }

    private static Task<Server> ConnectAsync(RawTmuxTestContext raw, CancellationToken token) =>
        Server.ConnectAsync(
            new ServerConnectionOptions(
                tmuxBinaryPath: raw.TmuxBinaryPath,
                socketPath: raw.SocketPath,
                configurationFile: "/dev/null"),
            token);
}
