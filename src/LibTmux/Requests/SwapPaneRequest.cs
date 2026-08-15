namespace LibTmux;

/// <summary>Names which neighbouring pane a swap uses.</summary>
public enum PaneSwapDirection
{
    /// <summary>The pane above.</summary>
    Up = 0,

    /// <summary>The pane below.</summary>
    Down = 1,
}

/// <summary>Describes one <c>swap-pane</c> invocation.</summary>
/// <remarks>
/// tmux replaces a named source with the neighbour whenever a direction is
/// given, so sending both would quietly drop the name.
/// </remarks>
public sealed record SwapPaneRequest
{
    /// <summary>Initializes a pane-swap request.</summary>
    /// <param name="target">The pane to swap with.</param>
    /// <param name="direction">The neighbour to swap with instead.</param>
    /// <param name="detach">Whether the swapped pane is left unselected.</param>
    /// <param name="keepZoom">Whether a zoomed pane stays zoomed.</param>
    /// <exception cref="ArgumentException">
    /// Neither a target nor a direction is given, or both are.
    /// </exception>
    public SwapPaneRequest(
        string? target = null,
        PaneSwapDirection? direction = null,
        bool detach = false,
        bool keepZoom = false)
    {
        if ((target is null) == (direction is null))
        {
            throw new ArgumentException(
                "A swap names a pane or a direction, not both and not neither.",
                nameof(target));
        }

        if (direction is not null && !Enum.IsDefined(direction.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        Target = target;
        Direction = direction;
        Detach = detach;
        KeepZoom = keepZoom;
    }

    /// <summary>Gets the pane to swap with.</summary>
    public string? Target { get; }

    /// <summary>Gets the neighbour to swap with instead.</summary>
    public PaneSwapDirection? Direction { get; }

    /// <summary>Gets whether the swapped pane is left unselected.</summary>
    public bool Detach { get; }

    /// <summary>Gets whether a zoomed pane stays zoomed.</summary>
    public bool KeepZoom { get; }
}
