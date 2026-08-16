using System.Runtime.Versioning;
using LibTmux.IntegrationTests.Infrastructure;
using LibTmux.IntegrationTests.Transport;

namespace LibTmux.IntegrationTests.Chaining;

/// <summary>Proves a chain refuses a target from a server that has since restarted.</summary>
/// <remarks>
/// tmux reuses IDs. A pane called <c>%0</c> on a restarted server is a different
/// pane from the <c>%0</c> a handle was read from, so a stale handle aimed at a
/// live server does not fail -- it succeeds against the wrong object, which is
/// the failure mode worth preventing.
///
/// The one-shot path has always guarded this. These tests exist because chaining
/// did not: the command carried the target as text and nothing said which server
/// the text came from.
/// </remarks>
[UnsupportedOSPlatform("windows")]
public sealed class ChainGenerationTests
{
    [UnixFact]
    public async Task A_chained_entity_command_is_refused_after_the_server_restarts()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;

        Server first = await ConnectAsync(raw, token);
        Session session = (await first.GetSessionsAsync(token))[0];
        Window window = (await session.GetWindowsAsync(token))[0];
        Pane pane = (await window.GetPanesAsync(token))[0];

        // The server this pane was read from goes away, and a new one takes the
        // same socket and hands out the same IDs.
        await first.KillAsync(token);
        await raw.ExecuteAsync(["new-session", "-d", "-s", "replacement"], token);
        Server second = await ConnectAsync(raw, token);

        // The chain is built on the new server from a handle belonging to the
        // old one. That is the shape a caller reaches by accident: entity
        // handles outlive the server far more easily than they look like they do.
        TmuxChain chain = second.Chain()
            .Then(new SendKeysRequest("echo stale").ToCommand(pane));

        await Assert.ThrowsAsync<StaleServerGenerationException>(
            () => chain.ExecuteAsync(token));
    }

    [UnixFact]
    public async Task A_chain_mixing_two_servers_is_refused_before_anything_runs()
    {
        await using RawTmuxTestContext first = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        await using RawTmuxTestContext second = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;

        Server one = await ConnectAsync(first, token);
        Server two = await ConnectAsync(second, token);
        Pane fromOne = (await (await (await one.GetSessionsAsync(token))[0]
            .GetWindowsAsync(token))[0].GetPanesAsync(token))[0];
        Pane fromTwo = (await (await (await two.GetSessionsAsync(token))[0]
            .GetWindowsAsync(token))[0].GetPanesAsync(token))[0];

        // At most one of these servers can be the one the chain runs against, so
        // there is no execution that satisfies both commands. Saying so costs
        // nothing; discovering it by running half the chain costs a side effect.
        TmuxChain chain = one.Chain()
            .Then(new SendKeysRequest("echo one").ToCommand(fromOne))
            .Then(new SendKeysRequest("echo two").ToCommand(fromTwo));

        InvalidOperationException failure =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => chain.ExecuteAsync(token));

        Assert.Contains("generation", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [UnixFact]
    public async Task A_chain_of_entity_commands_still_runs_on_the_server_it_came_from()
    {
        await using RawTmuxTestContext raw = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        CancellationToken token = TestContext.Current.CancellationToken;

        Server server = await ConnectAsync(raw, token);
        Pane pane = (await (await (await server.GetSessionsAsync(token))[0]
            .GetWindowsAsync(token))[0].GetPanesAsync(token))[0];

        // The guard has to be invisible when nothing is stale, or it would just
        // be a way to break chaining.
        await server.Chain()
            .Then(new SendKeysRequest("echo fresh").ToCommand(pane))
            .ExecuteAsync(token);
    }

    private static Task<Server> ConnectAsync(RawTmuxTestContext raw, CancellationToken token) =>
        Server.ConnectAsync(
            new ServerConnectionOptions(
                tmuxBinaryPath: raw.TmuxBinaryPath,
                socketPath: raw.SocketPath,
                configurationFile: "/dev/null"),
            token);
}
