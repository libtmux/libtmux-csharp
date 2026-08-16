using System.Runtime.Versioning;
using LibTmux.Examples;

namespace LibTmux.ExampleTests;

/// <summary>Holds the isolation every example depends on.</summary>
/// <remarks>
/// These assert the property rather than the code that provides it: a socket
/// this repository owns, named for the example that made it, reachable at a
/// path under this repository's own root. A regression here does not break an
/// example — it breaks whatever else on the machine happens to be running
/// tmux, which is the failure nobody attributes correctly.
/// </remarks>
[Collection("Examples")]
[UnsupportedOSPlatform("windows")]
public sealed class ExampleNamespaceTests
{
    [Fact]
    public async Task A_namespace_names_its_own_socket_and_finds_it_under_our_root()
    {
        await using ExampleNamespace world = await ExampleNamespace.EnterAsync(
            "IsolationProof",
            TestContext.Current.CancellationToken);

        Assert.StartsWith(ExampleNamespace.SocketPrefix, world.SocketName, StringComparison.Ordinal);
        Assert.Contains("IsolationProof", world.SocketName, StringComparison.Ordinal);

        string? socket = ExampleNamespace.FindSocket(world.SocketName);
        Assert.NotNull(socket);
        Assert.StartsWith(ExampleNamespace.SocketRoot, socket, StringComparison.Ordinal);

        // The property every published example rests on: a connect that names
        // nothing lands on this namespace's socket, not on the developer's.
        Server bare = await Server.ConnectAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(bare.IsMaterialized);
        Assert.NotEmpty(await bare.GetSessionsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_namespace_can_never_be_the_developers_own_server()
    {
        // "default" is the socket a bare tmux uses, so an example that could
        // land on it could kill the session its author is sitting in.
        await using ExampleNamespace world = await ExampleNamespace.EnterAsync(
            "NotDefault",
            TestContext.Current.CancellationToken);

        Assert.NotEqual("default", world.SocketName);
    }

    [Fact]
    public async Task A_namespace_puts_back_every_variable_it_moved()
    {
        string?[] before = Read();

        await using (await ExampleNamespace.EnterAsync(
            "RestoresEnvironment",
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(
                ExampleNamespace.SocketRoot,
                Environment.GetEnvironmentVariable("TMUX_TMPDIR"));
            Assert.NotNull(Environment.GetEnvironmentVariable("LIBTMUX_SOCKET_NAME"));
        }

        Assert.Equal(before, Read());
    }

    [Fact]
    public async Task A_namespace_leaves_no_socket_and_no_directory_behind()
    {
        string name;
        string directory;
        await using (ExampleNamespace world = await ExampleNamespace.EnterAsync(
            "LeavesNothing",
            TestContext.Current.CancellationToken))
        {
            name = world.SocketName;
            directory = world.Directory;
            Assert.True(Directory.Exists(directory));
        }

        Assert.Null(ExampleNamespace.FindSocket(name));
        Assert.False(Directory.Exists(directory));
    }

    private static readonly string[] Moved =
    [
        "TMUX_TMPDIR",
        "TMPDIR",
        "LIBTMUX_SOCKET_NAME",
        "LIBTMUX_SOCKET_PATH",
        "TMUX",
        "TMUX_PANE",
    ];

    private static string?[] Read() => [.. Moved.Select(Environment.GetEnvironmentVariable)];
}
