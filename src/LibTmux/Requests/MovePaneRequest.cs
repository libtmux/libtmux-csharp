namespace LibTmux;

/// <summary>Describes one <c>move-pane</c> or <c>join-pane</c> invocation.</summary>
public sealed record MovePaneRequest
{
    /// <summary>Initializes a pane-move request.</summary>
    /// <param name="target">The pane or window to move against.</param>
    /// <param name="direction">Which side of the target the pane lands on.</param>
    /// <param name="size">The size in cells or as a percentage.</param>
    /// <param name="detach">Whether the moved pane is left unselected.</param>
    /// <param name="fullWindow">Whether the split spans the whole window.</param>
    /// <param name="before">Whether the pane lands before the target.</param>
    /// <exception cref="ArgumentException"><paramref name="target" /> is blank.</exception>
    public MovePaneRequest(
        string target,
        PaneDirection direction = PaneDirection.Below,
        string? size = null,
        bool detach = true,
        bool fullWindow = false,
        bool before = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        Target = target;
        Direction = direction;
        Size = size;
        Detach = detach;
        FullWindow = fullWindow;
        Before = before;
    }

    /// <summary>Gets the pane or window to move against.</summary>
    public string Target { get; }

    /// <summary>Gets which side of the target the pane lands on.</summary>
    /// <remarks>
    /// Only the axis comes from the direction; landing before the target is
    /// <see cref="Before" />, which an above or left direction implies.
    /// </remarks>
    public PaneDirection Direction { get; }

    /// <summary>Gets the size in cells or as a percentage.</summary>
    /// <remarks>
    /// Sent as one sizing flag on every supported tmux, because the percentage
    /// flag is broken from 3.4 through 3.6.
    /// </remarks>
    public string? Size { get; }

    /// <summary>Gets whether the moved pane is left unselected.</summary>
    public bool Detach { get; }

    /// <summary>Gets whether the split spans the whole window.</summary>
    public bool FullWindow { get; }

    /// <summary>Gets whether the pane lands before the target.</summary>
    public bool Before { get; }
}
