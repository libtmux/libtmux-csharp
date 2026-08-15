using System.Runtime.Versioning;
using LibTmux.Internal;

namespace LibTmux;

/// <summary>Reads what one capture of the server found.</summary>
/// <remarks>
/// These say what a capture found, and nothing else. Reading one never reaches
/// tmux, so walking a server's sessions and each session's windows costs the
/// commands the capture ran and not one per step. A handle that has captured
/// nothing answers an uncaptured relation rather than an empty one, because
/// "nobody looked" and "there are none" are different answers and a caller
/// acting on the second when the first is true would be wrong.
/// </remarks>
public sealed partial class Server
{
    private readonly ServerSnapshot? _snapshot;

    private Server(
        TmuxConnection connection,
        ServerGeneration? generation,
        string? rawVersion,
        ServerSnapshot snapshot)
        : this(connection, generation, rawVersion) =>
        _snapshot = snapshot;

    /// <summary>Gets the sessions this handle captured.</summary>
    public CapturedRelation<Session> Sessions =>
        _snapshot?.Sessions ?? CapturedRelation.Uncaptured<Session>("sessions", Depth);

    /// <summary>Gets the windows this handle captured, across every session.</summary>
    /// <remarks>
    /// A window linked into several sessions was read once per session, so it
    /// appears here once per session it is linked into. Which session each one
    /// belongs to is read from the window rather than from this list.
    /// </remarks>
    public CapturedRelation<Window> Windows =>
        _snapshot?.Windows ?? CapturedRelation.Uncaptured<Window>("windows", Depth);

    /// <summary>Gets the panes this handle captured, across every window.</summary>
    /// <remarks>
    /// tmux lists panes per window rather than per server, so a capture that
    /// stopped short of panes leaves this uncaptured even when the windows are
    /// there.
    /// </remarks>
    public CapturedRelation<Pane> Panes =>
        _snapshot?.Panes ?? CapturedRelation.Uncaptured<Pane>("panes", Depth);

    /// <summary>Gets the clients this handle captured.</summary>
    /// <remarks>
    /// A capture reads the hierarchy, which clients are not part of: a client
    /// is attached to a session rather than contained by one. Reading them is
    /// <see cref="GetClientsAsync" />.
    /// </remarks>
    public CapturedRelation<Client> Clients =>
        CapturedRelation.Uncaptured<Client>("clients", Depth);

    /// <summary>Reads the server and answers a handle carrying what it found.</summary>
    /// <param name="depth">How far down the hierarchy to read.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>A handle whose relations are the ones this reading found.</returns>
    /// <exception cref="InvalidOperationException">The handle has no connection.</exception>
    /// <remarks>
    /// A handle that has not yet found a live server discovers one first. A
    /// scope hands back the endpoint it started rather than a materialized
    /// handle, because a tmux server with no sessions exits at once and there
    /// is nothing to discover until the first session exists. Since handles
    /// are immutable, the scope cannot materialize its own later, so requiring
    /// it here would make the obvious call the wrong one.
    /// </remarks>
    [UnsupportedOSPlatform("windows")]
    public async Task<Server> CaptureSnapshotAsync(
        SnapshotDepth depth = SnapshotDepth.Panes,
        CancellationToken cancellationToken = default)
    {
        TmuxConnection connection = _connection
            ?? throw new InvalidOperationException("The server handle has no connection.");
        Server live = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        ServerSnapshot snapshot = await ServerSnapshot
            .CaptureAsync(live, depth, cancellationToken)
            .ConfigureAwait(false);

        // The capture belongs to a new handle. Changing this one would make a
        // handle somebody already holds start answering differently. The new
        // one carries the generation the capture actually read, which is what
        // makes a stale capture detectable rather than merely old.
        return new Server(connection, live.Generation, live.RawVersion, snapshot);
    }

    internal ServerSnapshot? Snapshot => _snapshot;

    private SnapshotDepth Depth => _snapshot?.Depth ?? SnapshotDepth.Server;
}
