namespace LibTmux;

/// <summary>Names how far down the tmux hierarchy a snapshot captured.</summary>
/// <remarks>
/// Depth is recorded rather than inferred so a caller can tell an empty level
/// apart from a level that was never read.
/// </remarks>
public enum SnapshotDepth
{
    /// <summary>Only the server itself was captured.</summary>
    Server = 0,

    /// <summary>Sessions were captured, but not their windows.</summary>
    Sessions = 1,

    /// <summary>Sessions and their windows were captured.</summary>
    Windows = 2,

    /// <summary>Sessions, windows, and their panes were captured.</summary>
    Panes = 3,
}
