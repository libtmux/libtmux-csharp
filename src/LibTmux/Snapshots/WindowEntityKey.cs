namespace LibTmux;

/// <summary>Identifies one window as it appears inside one session.</summary>
/// <remarks>
/// tmux can link a single window into several sessions at different indexes,
/// so a window identifier alone does not name a position in the hierarchy.
/// </remarks>
/// <param name="SessionId">The session the window is linked into.</param>
/// <param name="WindowId">The linked window.</param>
public readonly record struct WindowEntityKey(SessionId SessionId, WindowId WindowId)
{
    /// <inheritdoc />
    public override string ToString() => $"{SessionId}:{WindowId}";
}
