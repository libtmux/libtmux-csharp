using System.Runtime.Versioning;
using LibTmux.Engineering;

namespace LibTmux.Examples;

/// <summary>A tmux world of one example's own, with a server already in it.</summary>
/// <remarks>
/// An example is worth publishing when its first line is the line worth
/// teaching — <c>Server.ConnectAsync()</c>, not the four lines of harness that
/// gave it somewhere to connect to. That is only possible if the harness is
/// ambient, so this is it: a socket root nothing else writes to, a server
/// already listening on <c>default</c> inside it, and the variables that would
/// point tmux at the developer's own session cleared for the duration.
///
/// Every path tmux uses to find a socket is moved, because there are two:
/// <c>TMUX_TMPDIR</c> for a <c>-L</c> name, and the temporary directory for a
/// <c>-S</c> path, which ignores <c>TMUX_TMPDIR</c> entirely. Both land under
/// this repository's own root, so an example can never reach the socket
/// another libtmux port is using.
///
/// The environment is process-wide, which is what makes a bare connect work
/// and what makes examples run one at a time.
/// </remarks>
[UnsupportedOSPlatform("windows")]
public sealed class ExampleNamespace : IAsyncDisposable
{
    private static readonly string[] Variables =
        ["TMUX_TMPDIR", "TMPDIR", "TMUX", "TMUX_PANE"];

    private readonly string _root;
    private readonly string _directory;
    private readonly OwnedServerScope _server;
    private readonly IReadOnlyList<(string Name, string? Value)> _restore;
    private int _disposed;

    private ExampleNamespace(
        string root,
        string directory,
        OwnedServerScope server,
        IReadOnlyList<(string Name, string? Value)> restore)
    {
        _root = root;
        _directory = directory;
        _server = server;
        _restore = restore;
    }

    /// <summary>Gets the server this example may reach.</summary>
    public Server Server => _server.Value;

    /// <summary>Gets the session waiting on that server.</summary>
    public Session Session { get; private set; } = null!;

    /// <summary>Gets that session's window.</summary>
    public Window Window { get; private set; } = null!;

    /// <summary>Gets that window's pane.</summary>
    public Pane Pane { get; private set; } = null!;

    /// <summary>Gets the directory every socket in this namespace lives under.</summary>
    public string SocketRoot => _root;

    /// <summary>Opens a namespace named for the example about to run.</summary>
    /// <param name="name">The example's name, which names its directory too.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>The namespace, which kills its server when disposed.</returns>
    public static async Task<ExampleNamespace> EnterAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string root = Path.Combine(
            WorkspaceSocketRoot.Root,
            "examples",
            string.Concat(name, "-", Guid.NewGuid().ToString("N")[..8]));
        Directory.CreateDirectory(root);

        // Read before writing: a namespace that cannot put back what it found
        // would leak its root into whatever runs next in this process.
        List<(string Name, string? Value)> restore =
        [
            .. Variables.Select(
                variable => (variable, Environment.GetEnvironmentVariable(variable))),
        ];

        // TMUX and TMUX_PANE are cleared rather than moved. Inherited, they
        // point a new client at the server the developer is sitting in and at
        // a pane that has nothing to do with the example.
        Environment.SetEnvironmentVariable("TMUX_TMPDIR", root);
        Environment.SetEnvironmentVariable("TMPDIR", root);
        Environment.SetEnvironmentVariable("TMUX", null);
        Environment.SetEnvironmentVariable("TMUX_PANE", null);

        // A pane inherits the directory the server was started from, and an
        // example that types a build command should not be typing it into the
        // source tree. This is the same reason the sockets moved.
        string directory = Environment.CurrentDirectory;
        Environment.CurrentDirectory = root;

        try
        {
            OwnedServerScope server = await Server.CreateOwnedAsync(
                cancellationToken: cancellationToken);

            // The socket this owns is named "default", which is also the name
            // of the developer's own server. Owning one means killing it on
            // the way out, so nothing is owned until the socket has been found
            // inside this namespace's root. A server that turned up anywhere
            // else is left running: the scope is deliberately not disposed.
            if (!HasSocketUnder(root))
            {
                throw new InvalidOperationException(
                    $"The tmux server for example '{name}' did not start under {root}. "
                    + "Refusing to take ownership of a server that may be the "
                    + "developer's own, and leaving it running.");
            }

            ExampleNamespace world = new(root, directory, server, restore);
            await world.PopulateAsync(cancellationToken);
            return world;
        }
        catch
        {
            Environment.CurrentDirectory = directory;
            Restore(restore);
            TryDelete(root);
            throw;
        }
    }

    private async Task PopulateAsync(CancellationToken cancellationToken)
    {
        // A server nobody has made a session on is not what a reader's machine
        // looks like, and control mode has nothing to attach to on one.
        Session = await Server.CreateSessionAsync(
            new NewSessionRequest(name: "example"),
            cancellationToken);
        Window = (await Session.GetWindowsAsync(cancellationToken))[0];
        Pane = (await Window.GetPanesAsync(cancellationToken))[0];
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _server.DisposeAsync();
        }
        finally
        {
            Environment.CurrentDirectory = _directory;
            Restore(_restore);
            TryDelete(_root);
        }
    }

    private static bool HasSocketUnder(string root) =>
        Directory.EnumerateDirectories(root, "tmux-*")
            .Any(directory => File.Exists(Path.Combine(directory, "default")));

    private static void Restore(IReadOnlyList<(string Name, string? Value)> restore)
    {
        foreach ((string name, string? value) in restore)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static void TryDelete(string root)
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
            // A socket the kernel has not finished releasing is not a failed
            // example, and the root is this repository's own to sweep.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
