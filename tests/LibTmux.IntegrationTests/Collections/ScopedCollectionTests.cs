using System.Runtime.Versioning;
using LibTmux.IntegrationTests.Infrastructure;
using LibTmux.IntegrationTests.Transport;

namespace LibTmux.IntegrationTests.Collections;

[UnsupportedOSPlatform("windows")]
public sealed class ScopedCollectionTests
{
    [Fact(
        Skip = "Requires a Unix process environment.",
        SkipType = typeof(UnixTestEnvironment),
        SkipUnless = nameof(UnixTestEnvironment.IsUnix))]
    public async Task Server_collections_span_every_session()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;
        Server server = await ConnectAsync(raw, token);
        await raw.ExecuteAsync(["new-session", "-d", "-s", "second"], token);
        await raw.ExecuteAsync(["new-window", "-t", "second:"], token);

        IReadOnlyList<Session> sessions = await server.GetSessionsAsync(token);
        IReadOnlyList<Window> windows = await server.GetWindowsAsync(token);
        IReadOnlyList<Pane> panes = await server.GetPanesAsync(token);

        // Server-wide listings cross session boundaries; a per-session listing
        // would report only one of these.
        Assert.Equal(2, sessions.Count);
        Assert.Equal(3, windows.Count);
        Assert.Equal(3, panes.Count);
        Assert.Equal(2, (await sessions[1].GetWindowsAsync(token)).Count);
    }

    [Fact(
        Skip = "Requires a Unix process environment.",
        SkipType = typeof(UnixTestEnvironment),
        SkipUnless = nameof(UnixTestEnvironment.IsUnix))]
    public async Task Attached_sessions_are_empty_without_a_client()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;
        Server server = await ConnectAsync(raw, token);

        Assert.NotEmpty(await server.GetSessionsAsync(token));
        Assert.Empty(await server.GetAttachedSessionsAsync(token));
    }

    [Fact(
        Skip = "Requires a Unix process environment.",
        SkipType = typeof(UnixTestEnvironment),
        SkipUnless = nameof(UnixTestEnvironment.IsUnix))]
    public async Task A_dead_server_lists_empty_rather_than_throwing()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;
        Server server = await ConnectAsync(raw, token);
        await raw.ExecuteAsync(["kill-server"], token);

        // A listing answers "what is there", so an absent daemon is an empty
        // answer rather than a failure the caller must handle.
        Assert.Empty(await server.GetSessionsAsync(token));
        Assert.Empty(await server.GetAttachedSessionsAsync(token));
        Assert.Empty(await server.GetWindowsAsync(token));
        Assert.Empty(await server.GetPanesAsync(token));
    }

    [Fact(
        Skip = "Requires a Unix process environment.",
        SkipType = typeof(UnixTestEnvironment),
        SkipUnless = nameof(UnixTestEnvironment.IsUnix))]
    public async Task List_accessors_are_lenient_on_tmux_errors()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;
        Server server = await ConnectAsync(raw, token);

        // A socket the caller cannot open is a live server answering with a
        // refusal, which reaches tmux by a different route than a daemon that
        // exited and prints different text. Leniency is a promise about every
        // way a listing can fail, so it is worth more than one of them.
        File.SetUnixFileMode(raw.SocketPath, UnixFileMode.None);
        try
        {
            Assert.Empty(await server.GetSessionsAsync(token));
            Assert.Empty(await server.GetAttachedSessionsAsync(token));
            Assert.Empty(await server.GetWindowsAsync(token));
            Assert.Empty(await server.GetPanesAsync(token));
        }
        finally
        {
            File.SetUnixFileMode(
                raw.SocketPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        // The socket replaced by an ordinary file is a third route, and one a
        // caller reaches by pointing at a stale path something else reused.
        await raw.ExecuteAsync(["kill-server"], token);
        File.Delete(raw.SocketPath);
        File.WriteAllText(raw.SocketPath, string.Empty);

        Assert.Empty(await server.GetSessionsAsync(token));
        Assert.Empty(await server.GetAttachedSessionsAsync(token));
        Assert.Empty(await server.GetWindowsAsync(token));
        Assert.Empty(await server.GetPanesAsync(token));
    }

    [Fact(
        Skip = "Requires a Unix process environment.",
        SkipType = typeof(UnixTestEnvironment),
        SkipUnless = nameof(UnixTestEnvironment.IsUnix))]
    public async Task Explicit_liveness_checks_preserve_failures()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;
        Server server = await ConnectAsync(raw, token);

        Assert.True(await server.IsAliveAsync(token));
        await raw.ExecuteAsync(["kill-server"], token);

        // An empty listing is what a server with no sessions returns too, so
        // the caller who needs the difference has to ask a question leniency
        // cannot answer. What the lenient path discarded is here in full.
        Assert.Empty(await server.GetSessionsAsync(token));
        Assert.False(await server.IsAliveAsync(token));

        TmuxCommandException failure = await Assert.ThrowsAsync<TmuxCommandException>(
            async () => await server.RaiseIfDeadAsync(token));

        Assert.NotEqual(0, failure.Result.ExitCode);
        Assert.Contains(
            failure.Result.StandardErrorLines,
            static line => line.Contains("no server running", StringComparison.Ordinal));
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
