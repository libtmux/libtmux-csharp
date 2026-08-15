namespace LibTmux;

/// <summary>Thrown when a snapshot never captured the requested relation.</summary>
public sealed class IncompleteSnapshotException : LibTmuxException
{
    /// <summary>Initializes the exception for one uncaptured relation.</summary>
    /// <param name="relation">The relation that was not captured.</param>
    /// <param name="capturedDepth">The depth the snapshot reached.</param>
    public IncompleteSnapshotException(string relation, SnapshotDepth capturedDepth)
        : base($"The snapshot captured {capturedDepth} and cannot supply '{relation}'.")
    {
        Relation = relation;
        CapturedDepth = capturedDepth;
    }

    /// <summary>Gets the relation the caller asked for.</summary>
    public string Relation { get; } = string.Empty;

    /// <summary>Gets the depth the snapshot actually reached.</summary>
    public SnapshotDepth CapturedDepth { get; }
}
