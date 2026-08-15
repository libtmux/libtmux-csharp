namespace LibTmux;

/// <summary>Places one window at one index inside one session.</summary>
/// <remarks>
/// A window linked into several sessions has one edge per session, so the
/// index belongs to the edge rather than to the window. The ordinal stays
/// null until a snapshot orders a session's edges.
/// </remarks>
public sealed record SessionWindowEdge
{
    /// <summary>Gets the session the window is linked into.</summary>
    public required SessionId SessionId { get; init; }

    /// <summary>Gets the linked window.</summary>
    public required WindowId WindowId { get; init; }

    /// <summary>Gets the tmux window index inside the session.</summary>
    public required int WindowIndex { get; init; }

    /// <summary>Gets the edge's position in the session's window order.</summary>
    public int? Ordinal { get; init; }

    /// <summary>Gets the session and window this edge joins.</summary>
    public WindowEntityKey Key => new(SessionId, WindowId);
}
