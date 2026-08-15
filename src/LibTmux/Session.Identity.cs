using System.Runtime.Versioning;
using LibTmux.Internal;

namespace LibTmux;

/// <summary>Provides typed session identity.</summary>
public sealed partial class Session
{
    private readonly SessionId _id;
    private readonly ServerGeneration _generation;
    private readonly IReadOnlyDictionary<string, string?>? _snapshot;

    [UnsupportedOSPlatform("windows")]
    internal Session(TmuxConnection connection, ServerGeneration generation, SessionId id)
        : this(connection.CreateEntityDispatcher(generation), TmuxTarget.From(id).Value)
    {
        _id = id;
        _generation = generation;
    }

    [UnsupportedOSPlatform("windows")]
    internal Session(
        TmuxConnection connection,
        ServerGeneration generation,
        SessionId id,
        IReadOnlyDictionary<string, string?> snapshot)
        : this(connection, generation, id)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshot = snapshot;
    }

    /// <summary>Gets the tmux fields captured when this handle materialized.</summary>
    /// <remarks>
    /// A handle resolved by identifier alone carries no snapshot, so callers
    /// must ask whether one was captured rather than read empty fields.
    /// </remarks>
    internal IReadOnlyDictionary<string, string?>? Snapshot => _snapshot;

    /// <summary>Gets the tmux fields captured when this handle materialized.</summary>
    /// <exception cref="IncompleteSnapshotException">
    /// The session was resolved by identifier rather than materialized.
    /// </exception>
    public IReadOnlyDictionary<string, string?> RawFormatFields =>
        _snapshot ?? throw new IncompleteSnapshotException("format fields", SnapshotDepth.Sessions);

    /// <summary>Gets the session identifier.</summary>
    public SessionId Id => _id;

    /// <summary>Gets the server generation captured with this session.</summary>
    public ServerGeneration Generation => _generation;

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is Session other && _generation == other._generation && _id == other._id;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_generation, _id);
}
