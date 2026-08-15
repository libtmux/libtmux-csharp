namespace LibTmux;

/// <summary>Names which pane a selection moves to.</summary>
public enum PaneSelectDirection
{
    /// <summary>The pane above.</summary>
    Up = 0,

    /// <summary>The pane below.</summary>
    Down = 1,

    /// <summary>The pane to the left.</summary>
    Left = 2,

    /// <summary>The pane to the right.</summary>
    Right = 3,

    /// <summary>The pane that was last active.</summary>
    Last = 4,
}

/// <summary>Describes one <c>select-pane</c> invocation.</summary>
public sealed record SelectPaneRequest
{
    /// <summary>Initializes a pane-selection request.</summary>
    /// <param name="direction">Which pane to move to.</param>
    /// <param name="keepZoom">Whether a zoomed pane stays zoomed.</param>
    /// <param name="mark">Whether the pane is marked, unmarked, or left alone.</param>
    /// <param name="inputEnabled">Whether input is enabled, disabled, or left alone.</param>
    /// <param name="last">Whether the last active pane is selected.</param>
    public SelectPaneRequest(
        PaneSelectDirection? direction = null,
        bool keepZoom = false,
        bool? mark = null,
        bool? inputEnabled = null,
        bool last = false)
    {
        if (direction is not null && !Enum.IsDefined(direction.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        Direction = direction;
        KeepZoom = keepZoom;
        Mark = mark;
        InputEnabled = inputEnabled;
        Last = last;
    }

    /// <summary>Gets which pane to move to.</summary>
    /// <remarks>
    /// <see cref="PaneSelectDirection.Last" /> and <see cref="Last" /> are two
    /// spellings of the same tmux flag, which is sent once either way.
    /// </remarks>
    public PaneSelectDirection? Direction { get; }

    /// <summary>Gets whether a zoomed pane stays zoomed.</summary>
    public bool KeepZoom { get; }

    /// <summary>Gets whether the pane is marked, unmarked, or left alone.</summary>
    /// <remarks>Null omits both flags, so tmux leaves the mark as it is.</remarks>
    public bool? Mark { get; }

    /// <summary>Gets whether input is enabled, disabled, or left alone.</summary>
    /// <remarks>Null omits both flags, so tmux leaves input as it is.</remarks>
    public bool? InputEnabled { get; }

    /// <summary>Gets whether the last active pane is selected.</summary>
    public bool Last { get; }
}
