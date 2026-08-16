namespace LibTmux.Mcp;

/// <summary>How much of tmux this server is willing to expose.</summary>
/// <remarks>
/// The tiers are cumulative: a higher one offers everything the ones below it
/// do. A tool above the active tier is not registered, so it never reaches the
/// model's tool list and cannot be called by name.
/// </remarks>
public enum SafetyTier
{
    /// <summary>Tools that only read. Nothing they do changes tmux.</summary>
    ReadOnly = 0,

    /// <summary>Reading, plus creating and changing. Nothing is removed.</summary>
    Mutating = 1,

    /// <summary>Everything, including tools that remove what they act on.</summary>
    Destructive = 2,
}
