using System.Runtime.Versioning;
using LibTmux.Internal;

namespace LibTmux;

// Provides session hierarchy relations.
public sealed partial class Session
{
    private readonly Server? _owner;
    private Func<CapturedRelation<Window>>? _windows;
    private CapturedRelation<Pane>? _panes;

    [UnsupportedOSPlatform("windows")]
    internal Session(
        Server owner,
        TmuxConnection connection,
        ServerGeneration generation,
        SessionId id,
        IReadOnlyDictionary<string, string?> snapshot)
        : this(connection, generation, id, snapshot)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Gets the active window recorded when this session was read.</summary>
    /// <exception cref="IncompleteSnapshotException">
    /// The session was resolved by identifier rather than materialized.
    /// </exception>
    [UnsupportedOSPlatform("windows")]
    public Window ActiveWindow
    {
        get
        {
            if (!WindowId.TryParse(ReadSnapshot("window_id"), out WindowId id))
            {
                throw new IncompleteSnapshotException("active window", SnapshotDepth.Sessions);
            }

            return new Window(RequireConnection(), _generation, id);
        }
    }

    /// <summary>Gets the active pane recorded when this session was read.</summary>
    /// <exception cref="IncompleteSnapshotException">
    /// The session was resolved by identifier rather than materialized.
    /// </exception>
    [UnsupportedOSPlatform("windows")]
    public Pane ActivePane
    {
        get
        {
            if (!PaneId.TryParse(ReadSnapshot("pane_id"), out PaneId id))
            {
                throw new IncompleteSnapshotException("active pane", SnapshotDepth.Sessions);
            }

            return new Pane(RequireConnection(), _generation, id);
        }
    }

    /// <summary>Gets the windows the capture found in this session.</summary>
    /// <remarks>
    /// Reading this never reaches tmux. A handle that was not read from a
    /// capture answers uncaptured rather than empty, because "nobody looked"
    /// and "there are none" are different answers.
    /// </remarks>
    public CapturedRelation<Window> Windows =>
        _windows?.Invoke() ?? CapturedRelation.Uncaptured<Window>("windows", SnapshotDepth.Server);

    /// <summary>Gets the panes the capture found in this session.</summary>
    /// <remarks>Reading this never reaches tmux.</remarks>
    public CapturedRelation<Pane> Panes =>
        _panes ?? CapturedRelation.Uncaptured<Pane>("panes", SnapshotDepth.Server);

    /// <summary>Reads one of this session's windows by target.</summary>
    /// <param name="target">A tmux window target inside this session.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The window, or null when this session has no such window.</returns>
    /// <remarks>
    /// A target naming a window in another session answers null rather than
    /// that window: the question asked is which of this session's windows it
    /// is, and tmux resolving it elsewhere is not an answer to that.
    /// </remarks>
    [UnsupportedOSPlatform("windows")]
    public async Task<Window?> GetWindowAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        Server owner = RequireOwner("windows");
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows =
            await RelationReader.ListAsync(
                owner,
                "list-windows",
                ["-t", _id.ToString()],
                cancellationToken)
                .ConfigureAwait(false);
        IReadOnlyDictionary<string, string?>? match = rows.FirstOrDefault(row =>
            Matches(row, "window_id", target) || Matches(row, "window_name", target));
        return match is null ? null : RelationReader.ToWindow(owner, match);
    }

    private static bool Matches(
        IReadOnlyDictionary<string, string?> row,
        string wireName,
        string target) =>
        row.TryGetValue(wireName, out string? value)
        && string.Equals(value, target, StringComparison.Ordinal);

    /// <summary>Reads this session's windows from tmux.</summary>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The windows tmux reports for this session.</returns>
    [UnsupportedOSPlatform("windows")]
    public async Task<IReadOnlyList<Window>> GetWindowsAsync(
        CancellationToken cancellationToken = default)
    {
        Server owner = RequireOwner("windows");
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows =
            await RelationReader.ListAsync(owner, "list-windows", ["-t", _id.ToString()], cancellationToken)
                .ConfigureAwait(false);
        return [.. rows.Select(row => RelationReader.ToWindow(owner, row))];
    }

    /// <summary>Reads this session's panes from tmux.</summary>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The panes tmux reports for this session.</returns>
    [UnsupportedOSPlatform("windows")]
    public async Task<IReadOnlyList<Pane>> GetPanesAsync(
        CancellationToken cancellationToken = default)
    {
        Server owner = RequireOwner("panes");
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows =
            await RelationReader.ListAsync(owner, "list-panes", ["-s", "-t", _id.ToString()], cancellationToken)
                .ConfigureAwait(false);
        return [.. rows.Select(row => RelationReader.ToPane(owner, row))];
    }

    internal Session WithCaptured(
        Func<CapturedRelation<Window>> windows,
        CapturedRelation<Pane> panes)
    {
        _windows = windows;
        _panes = panes;
        return this;
    }

    private Server RequireOwner(string relation) =>
        _owner ?? throw new IncompleteSnapshotException(relation, SnapshotDepth.Server);

    private TmuxConnection RequireConnection() =>
        RequireOwner("connection").Connection
        ?? throw new IncompleteSnapshotException("connection", SnapshotDepth.Server);

    private string? ReadSnapshot(string wireName) =>
        _snapshot is not null && _snapshot.TryGetValue(wireName, out string? value)
            ? value
            : null;
}

/// <summary>Reads one live relation and rebuilds owned entity handles.</summary>
/// <remarks>
/// Relation reads go through the same projection and materializer as a
/// snapshot, so a live child carries the same fields a captured one does.
/// </remarks>
internal static class RelationReader
{
    [UnsupportedOSPlatform("windows")]
    internal static Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> ListAsync(
        Server owner,
        string listCommand,
        IReadOnlyList<string> extraArguments,
        CancellationToken cancellationToken)
    {
        var context = new MaterializationContext(owner, ParseVersion(owner));
        return new MaterializationQuery(context)
            .FetchAsync(listCommand, extraArguments, cancellationToken);
    }

    [UnsupportedOSPlatform("windows")]
    internal static Window ToWindow(Server owner, IReadOnlyDictionary<string, string?> row)
    {
        EntityMaterializationState state = Capture(owner, row);
        return new Window(
            owner,
            Connection(owner),
            state.Generation,
            state.WindowId ?? throw new InvalidDataException("tmux row carries no window."),
            state.RawFields);
    }

    [UnsupportedOSPlatform("windows")]
    internal static Pane ToPane(Server owner, IReadOnlyDictionary<string, string?> row)
    {
        EntityMaterializationState state = Capture(owner, row);
        if (!PaneId.TryParse(
                state.RawFields.TryGetValue("pane_id", out string? text) ? text : null,
                out PaneId id))
        {
            throw new InvalidDataException("tmux row carries no pane.");
        }

        return new Pane(owner, Connection(owner), state.Generation, id, state.RawFields);
    }

    [UnsupportedOSPlatform("windows")]
    internal static Session ToSession(Server owner, IReadOnlyDictionary<string, string?> row)
    {
        EntityMaterializationState state = Capture(owner, row);
        return new Session(
            owner,
            Connection(owner),
            state.Generation,
            state.SessionId ?? throw new InvalidDataException("tmux row carries no session."),
            state.RawFields);
    }

    private static EntityMaterializationState Capture(
        Server owner,
        IReadOnlyDictionary<string, string?> row) =>
        Materializer.CreateState(new MaterializationContext(owner, ParseVersion(owner)), row);

    private static TmuxConnection Connection(Server owner) =>
        owner.Connection
        ?? throw new InvalidOperationException("The server has no connection.");

    private static TmuxVersion ParseVersion(Server owner)
    {
        string raw = owner.RawVersion
            ?? throw new InvalidOperationException("The server reported no tmux version.");
        return TmuxVersion.Parse(
            raw.StartsWith("tmux ", StringComparison.Ordinal) ? raw[5..] : raw);
    }
}
