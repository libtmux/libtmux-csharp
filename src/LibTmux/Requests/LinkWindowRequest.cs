namespace LibTmux;

/// <summary>Describes one <c>link-window</c> invocation.</summary>
public sealed record LinkWindowRequest
{
    /// <summary>Initializes a window-link request.</summary>
    /// <param name="targetSession">The session the window is linked into.</param>
    /// <param name="targetIndex">The index to link at, or null for the next free one.</param>
    /// <param name="direction">Whether to insert before or after the target.</param>
    /// <param name="replaceExisting">Whether a window already at the index is replaced.</param>
    /// <param name="detach">Whether the linked window is left unselected.</param>
    /// <exception cref="ArgumentException"><paramref name="targetSession" /> is blank.</exception>
    public LinkWindowRequest(
        string targetSession,
        string? targetIndex = null,
        WindowDirection? direction = null,
        bool replaceExisting = false,
        bool detach = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSession);
        TargetSession = targetSession;
        TargetIndex = targetIndex;
        Direction = direction;
        ReplaceExisting = replaceExisting;
        Detach = detach;
    }

    /// <summary>Gets the session the window is linked into.</summary>
    public string TargetSession { get; }

    /// <summary>Gets the index to link at, or null for the next free one.</summary>
    public string? TargetIndex { get; }

    /// <summary>Gets whether to insert before or after the target.</summary>
    /// <remarks>
    /// Without an index tmux inserts relative to the destination session's
    /// current window, not its first or last.
    /// </remarks>
    public WindowDirection? Direction { get; }

    /// <summary>Gets whether a window already at the index is replaced.</summary>
    public bool ReplaceExisting { get; }

    /// <summary>Gets whether the linked window is left unselected.</summary>
    public bool Detach { get; }
}
