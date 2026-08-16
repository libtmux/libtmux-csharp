using System.Runtime.Versioning;
using LibTmux.IntegrationTests.Transport;
using LibTmux.Mcp;
using LibTmux.Testing;

namespace LibTmux.IntegrationTests;

/// <summary>Telling a subscriber that the hierarchy is not what it was.</summary>
/// <remarks>
/// Driven directly rather than through a client. How a change reaches a client
/// is the protocol's business and it moves — the 2026-07-28 revision replaced
/// <c>resources/subscribe</c> with <c>subscriptions/listen</c>. What must keep
/// working across that is the part below: tmux says a window appeared, and a
/// subscriber is told.
/// </remarks>
/// <summary>Serializes the tests that hold a tmux control client.</summary>
/// <remarks>
/// A control client is a tmux process attached for as long as a test runs, and
/// several at once slow the machine enough to change what unrelated tests see.
/// The first symptom is always a test waiting on a shell to redraw, because
/// those carry the tightest budgets.
///
/// Measured after the harness stopped leaking servers, which was the larger
/// half of the same problem: serialized, four runs of the suite were clean;
/// parallel, one run in three failed.
/// </remarks>
[CollectionDefinition("tmux control clients", DisableParallelization = true)]
public sealed class ControlClientCollectionDefinition;

/// <content>Telling a subscriber the hierarchy changed.</content>
[Collection("tmux control clients")]
[UnsupportedOSPlatform("windows")]
public sealed class HierarchyWatcherTests
{
    [UnixFact]
    public async Task A_window_appearing_reaches_a_subscriber()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TmuxTestFactory factory = new();
        TmuxTestOptions options = new(new ServerConnectionOptions(
            tmuxBinaryPath: System.Environment.GetEnvironmentVariable("LIBTMUX_TMUX") ?? "tmux",
            socketName: $"ltw-{Guid.NewGuid():N}"[..20],
            configurationFile: "/dev/null"));
        await using TemporaryHierarchyScope scope = await factory.CreateHierarchyAsync(
            options,
            token);

        await using HierarchyWatcher watcher = new();
        TaskCompletionSource<IReadOnlyList<string>> told = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await watcher.SubscribeAsync(
            "tmux://hierarchy",
            changed =>
            {
                told.TrySetResult(changed);
                return Task.CompletedTask;
            },
            scope.Session.Server,
            token);

        await scope.Session.CreateWindowAsync(
            new NewWindowRequest(name: "appeared"),
            token);

        Task finished = await Task.WhenAny(
            told.Task,
            Task.Delay(TimeSpan.FromSeconds(20), token));
        Assert.True(finished == told.Task, "the watcher never reported the new window");
        Assert.Contains("tmux://hierarchy", await told.Task);
    }

    [UnixFact]
    public async Task Dropping_the_last_subscriber_stops_the_control_client()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TmuxTestFactory factory = new();
        TmuxTestOptions options = new(new ServerConnectionOptions(
            tmuxBinaryPath: System.Environment.GetEnvironmentVariable("LIBTMUX_TMUX") ?? "tmux",
            socketName: $"ltw-{Guid.NewGuid():N}"[..20],
            configurationFile: "/dev/null"));
        await using TemporaryHierarchyScope scope = await factory.CreateHierarchyAsync(
            options,
            token);

        await using HierarchyWatcher watcher = new();
        await watcher.SubscribeAsync(
            "tmux://hierarchy",
            _ => Task.CompletedTask,
            scope.Session.Server,
            token);

        // A control client is a real attached client and shows up in the
        // user's own list-clients, so it must not outlive the subscription
        // that needed it.
        await watcher.UnsubscribeAsync("tmux://hierarchy");

        // Polled gently: every probe is a tmux process, and a tight interval
        // here spawns hundreds of them beside the rest of the suite.
        IReadOnlyList<Client> clients = await TmuxWait.UntilAsync(
            cancellation => scope.Session.Server.GetClientsAsync(cancellation),
            current => current.Count == 0,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(250),
            token);
        Assert.Empty(clients);
    }

    [Theory]
    [InlineData("window-add", true)]
    [InlineData("layout-change", true)]
    [InlineData("session-renamed", true)]
    [InlineData("output", false)]
    [InlineData("continue", false)]
    // A bell or a byte of pane output is not a change to the hierarchy. Waking
    // every subscriber for one would cost more than the polling the
    // subscription replaces.
    public void Only_a_change_to_what_exists_wakes_a_subscriber(string name, bool expected) =>
        Assert.Equal(expected, HierarchyWatcher.IsStructural(name));
}
