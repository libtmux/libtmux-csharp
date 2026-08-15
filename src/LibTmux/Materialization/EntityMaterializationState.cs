namespace LibTmux.Internal;

/// <summary>
/// Carries one materialized row's copied fields and its place in the tmux
/// hierarchy.
/// </summary>
/// <remarks>
/// Relation slots start uncaptured. Hierarchy capture belongs to the snapshot
/// layer, which replaces slots with a <see langword="with" /> expression
/// rather than reaching back into materialization.
/// </remarks>
internal sealed record EntityMaterializationState
{
    /// <summary>Gets the decoded tmux fields for one row.</summary>
    internal required IReadOnlyDictionary<string, string?> RawFields { get; init; }

    /// <summary>Gets the server that owns the materialized entity.</summary>
    internal required Server Server { get; init; }

    /// <summary>Gets the generation observed when the row was parsed.</summary>
    internal required ServerGeneration Generation { get; init; }

    /// <summary>Gets the parent session, when the row names one.</summary>
    internal SessionId? SessionId { get; init; }

    /// <summary>Gets the parent window, when the row names one.</summary>
    internal WindowId? WindowId { get; init; }

    /// <summary>Gets the session-to-window edge, uncaptured until linked.</summary>
    internal SessionWindowEdge? WindowEdge { get; init; }
}
