using System.Runtime.Versioning;
using LibTmux.Internal;

namespace LibTmux;

// Provides pane hierarchy relations captured with the pane.
public sealed partial class Pane
{
    private readonly Server? _owner;

    [UnsupportedOSPlatform("windows")]
    internal Pane(
        Server owner,
        TmuxConnection connection,
        ServerGeneration generation,
        PaneId id,
        IReadOnlyDictionary<string, string?> snapshot)
        : this(connection, generation, id, snapshot)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Gets the server that owns this pane.</summary>
    /// <exception cref="IncompleteSnapshotException">
    /// The pane was resolved by identifier rather than materialized.
    /// </exception>
    public Server Server =>
        _owner ?? throw new IncompleteSnapshotException("server", SnapshotDepth.Server);

    /// <summary>Gets the session containing this pane.</summary>
    /// <exception cref="IncompleteSnapshotException">
    /// The pane carries no captured session identity.
    /// </exception>
    [UnsupportedOSPlatform("windows")]
    public Session Session
    {
        get
        {
            if (!SessionId.TryParse(ReadSnapshot("session_id"), out SessionId id))
            {
                throw new IncompleteSnapshotException("session", SnapshotDepth.Server);
            }

            return new Session(RequireConnection(), _generation, id);
        }
    }

    /// <summary>Gets the window containing this pane.</summary>
    /// <exception cref="IncompleteSnapshotException">
    /// The pane carries no captured window identity.
    /// </exception>
    [UnsupportedOSPlatform("windows")]
    public Window Window
    {
        get
        {
            if (!WindowId.TryParse(ReadSnapshot("window_id"), out WindowId id))
            {
                throw new IncompleteSnapshotException("window", SnapshotDepth.Server);
            }

            return new Window(RequireConnection(), _generation, id);
        }
    }

    private TmuxConnection RequireConnection() =>
        Server.Connection
        ?? throw new IncompleteSnapshotException("connection", SnapshotDepth.Server);

    private string? ReadSnapshot(string wireName) =>
        _snapshot is not null && _snapshot.TryGetValue(wireName, out string? value)
            ? value
            : null;
}
