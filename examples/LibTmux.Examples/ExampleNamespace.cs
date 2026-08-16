using System.Runtime.Versioning;
using LibTmux.Engineering;

namespace LibTmux.Examples;

/// <summary>A tmux world of one example's own, with a server already in it.</summary>
/// <remarks>
/// An example is worth publishing when its first line is the line worth
/// teaching — <c>Server.ConnectAsync()</c>, not the four lines of harness that
/// gave it somewhere to connect to. That is only possible if the harness is
/// ambient, so this is it: a socket named for the example, a server already
/// listening on it, and the variables that would point tmux at the developer's
/// own session cleared for the duration.
///
/// The socket is namespaced by name and by path, which is what the Python
/// original does with <c>libtmux_test&lt;n&gt;</c> under <c>TMUX_TMPDIR</c>.
/// The name matters more than it looks: a server on the socket named
/// <c>default</c> is the developer's own server, and this kills what it starts.
/// A name of its own means it cannot be that server, whatever happens to the
/// environment.
///
/// Both ways tmux finds a socket are moved, because there are two:
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
    /// <summary>What every example socket is called before its own name.</summary>
    public const string SocketPrefix = "libtmux-example-";

    /// <summary>The directory every example socket lives under.</summary>
    public static readonly string SocketRoot = Path.Combine(
        WorkspaceSocketRoot.Root,
        "examples");

    /// <summary>How long a Unix socket path may be, less the room tmux needs.</summary>
    /// <remarks>
    /// A Unix socket address carries its path in a fixed 108-byte field on
    /// Linux and 104 on macOS, and tmux builds that path as
    /// <c>&lt;root&gt;/tmux-&lt;uid&gt;/&lt;name&gt;</c>. Exceeding it fails
    /// at bind time with a message about the address, which reads like
    /// anything except a name that grew too long.
    /// </remarks>
    private const int SocketPathLimit = 104;

    private static readonly string[] Variables =
    [
        "TMUX_TMPDIR",
        "TMPDIR",
        "LIBTMUX_SOCKET_NAME",
        "LIBTMUX_SOCKET_PATH",
        "TMUX",
        "TMUX_PANE",
    ];

    private readonly string _directory;
    private readonly string _entered;
    private readonly OwnedServerScope _server;
    private readonly IReadOnlyList<(string Name, string? Value)> _restore;
    private int _disposed;

    private ExampleNamespace(
        string socketName,
        string directory,
        string entered,
        OwnedServerScope server,
        IReadOnlyList<(string Name, string? Value)> restore)
    {
        SocketName = socketName;
        _directory = directory;
        _entered = entered;
        _server = server;
        _restore = restore;
    }

    /// <summary>Gets the socket this example's server is listening on.</summary>
    public string SocketName { get; }

    /// <summary>Gets the server this example may reach.</summary>
    public Server Server => _server.Value;

    /// <summary>Gets the session waiting on that server.</summary>
    public Session Session { get; private set; } = null!;

    /// <summary>Gets that session's window.</summary>
    public Window Window { get; private set; } = null!;

    /// <summary>Gets that window's pane.</summary>
    public Pane Pane { get; private set; } = null!;

    /// <summary>Gets the scratch directory this example runs in.</summary>
    public string Directory => _directory;

    /// <summary>Opens a namespace named for the example about to run.</summary>
    /// <param name="name">The example's name, which names its socket too.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>The namespace, which kills its server when disposed.</returns>
    public static async Task<ExampleNamespace> EnterAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string nonce = Guid.NewGuid().ToString("N")[..8];
        string socketName = BuildSocketName(name, nonce);
        string directory = Path.Combine(SocketRoot, $"{name}-{nonce}");
        System.IO.Directory.CreateDirectory(directory);

        // Read before writing: a namespace that cannot put back what it found
        // would leak its socket into whatever runs next in this process.
        List<(string Name, string? Value)> restore =
        [
            .. Variables.Select(
                variable => (variable, Environment.GetEnvironmentVariable(variable))),
        ];

        // TMUX and TMUX_PANE are cleared rather than moved. Inherited, they
        // point a new client at the server the developer is sitting in and at
        // a pane that has nothing to do with the example.
        Environment.SetEnvironmentVariable("TMUX_TMPDIR", SocketRoot);
        Environment.SetEnvironmentVariable("TMPDIR", directory);
        Environment.SetEnvironmentVariable("LIBTMUX_SOCKET_NAME", socketName);
        Environment.SetEnvironmentVariable("LIBTMUX_SOCKET_PATH", null);
        Environment.SetEnvironmentVariable("TMUX", null);
        Environment.SetEnvironmentVariable("TMUX_PANE", null);

        // A pane inherits the directory the server was started from, and an
        // example that types a build command should not be typing it into the
        // source tree. This is the same reason the sockets moved.
        string entered = Environment.CurrentDirectory;
        Environment.CurrentDirectory = directory;

        try
        {
            OwnedServerScope server = await Server.CreateOwnedAsync(
                cancellationToken: cancellationToken);

            // The environment is what put the server on this socket, and an
            // environment can be wrong. Nothing is owned -- and owning means
            // killing -- until the socket has been found where it was asked
            // for. A server anywhere else is left running.
            if (FindSocket(socketName) is null)
            {
                throw new InvalidOperationException(
                    $"The tmux server for example '{name}' did not start on a socket "
                    + $"named {socketName} under {SocketRoot}. Refusing to take ownership "
                    + "of a server that turned up somewhere else, and leaving it running.");
            }

            ExampleNamespace world = new(socketName, directory, entered, server, restore);
            await world.PopulateAsync(cancellationToken);
            return world;
        }
        catch
        {
            Environment.CurrentDirectory = entered;
            Restore(restore);
            TryDelete(directory);
            throw;
        }
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
            Environment.CurrentDirectory = _entered;
            Restore(_restore);

            // The socket file outlives the server that made it, and a stale one
            // is what makes the next run's listing lie about what is here.
            if (FindSocket(SocketName) is string socket)
            {
                TryDeleteFile(socket);
            }

            TryDelete(_directory);
        }
    }

    /// <summary>Finds the socket of a given name, wherever tmux put it.</summary>
    /// <remarks>
    /// tmux names the directory under the root for the effective user, and
    /// asking the runtime for that number costs a P/Invoke this does not need:
    /// there is one such directory and the socket is either in it or nowhere.
    /// </remarks>
    public static string? FindSocket(string socketName)
    {
        if (!System.IO.Directory.Exists(SocketRoot))
        {
            return null;
        }

        return System.IO.Directory
            .EnumerateDirectories(SocketRoot, "tmux-*")
            .Select(directory => Path.Combine(directory, socketName))
            .FirstOrDefault(File.Exists);
    }

    private static string BuildSocketName(string name, string nonce)
    {
        // The user directory is not known without asking the kernel, so this
        // budgets for the widest one rather than measuring: a uid is at most
        // ten digits, and being a few bytes pessimistic costs a shorter name.
        string prefix = Path.Combine(SocketRoot, "tmux-4294967295") + Path.DirectorySeparatorChar;
        int room = SocketPathLimit - prefix.Length - SocketPrefix.Length - nonce.Length - 1;
        if (room < 1)
        {
            throw new InvalidOperationException(
                $"There is no room for an example socket name under {SocketRoot}: a Unix "
                + $"socket path may be {SocketPathLimit} bytes and the root alone is "
                + $"{prefix.Length}.");
        }

        // The nonce is what keeps two runs apart, so the example's name is
        // what gives way. A truncated name still says which example this is.
        string trimmed = name.Length <= room ? name : name[..room];
        return $"{SocketPrefix}{trimmed}-{nonce}";
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

    private static void Restore(IReadOnlyList<(string Name, string? Value)> restore)
    {
        foreach ((string name, string? value) in restore)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string directory)
    {
        try
        {
            System.IO.Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // A file the kernel has not finished releasing is not a failed
            // example, and the root is this repository's own to sweep.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
