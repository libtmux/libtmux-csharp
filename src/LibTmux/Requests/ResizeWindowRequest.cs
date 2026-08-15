namespace LibTmux;

/// <summary>Names how a window is resized against its clients.</summary>
public enum WindowResizeMode
{
    /// <summary>Size the window to its largest client.</summary>
    Expand = 0,

    /// <summary>Size the window to its smallest client.</summary>
    Shrink = 1,
}

/// <summary>Describes one <c>resize-window</c> invocation.</summary>
/// <remarks>
/// tmux applies a mode after a direction or an explicit size and silently
/// discards the loser, so the request refuses the ambiguity instead.
/// </remarks>
public sealed record ResizeWindowRequest
{
    /// <summary>Initializes a window-resize request.</summary>
    /// <param name="direction">The edge to move.</param>
    /// <param name="adjustment">How many cells to move it by.</param>
    /// <param name="width">An explicit width.</param>
    /// <param name="height">An explicit height.</param>
    /// <param name="mode">Sizing against the window's clients instead.</param>
    /// <exception cref="ArgumentException">
    /// More than one of direction, explicit size, and mode is set, or a
    /// direction has no adjustment.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A supplied width, height, or adjustment is not positive.
    /// </exception>
    public ResizeWindowRequest(
        ResizeDirection? direction = null,
        int? adjustment = null,
        int? width = null,
        int? height = null,
        WindowResizeMode? mode = null)
    {
        bool hasSize = width is not null || height is not null;
        int primaries = (direction is null ? 0 : 1) + (hasSize ? 1 : 0) + (mode is null ? 0 : 1);
        if (primaries > 1)
        {
            throw new ArgumentException(
                "A resize moves an edge, sets a size, or follows the clients; not more than one.",
                nameof(mode));
        }

        if (direction is null && adjustment is not null)
        {
            throw new ArgumentException(
                "An adjustment has no meaning without a direction to apply it to.",
                nameof(adjustment));
        }

        if (direction is not null && adjustment is null)
        {
            throw new ArgumentException(
                "A direction needs an adjustment to move by.",
                nameof(adjustment));
        }

        if (direction is not null && !Enum.IsDefined(direction.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        if (mode is not null && !Enum.IsDefined(mode.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        ThrowIfNotPositive(adjustment, nameof(adjustment));
        ThrowIfNotPositive(width, nameof(width));
        ThrowIfNotPositive(height, nameof(height));

        Direction = direction;
        Adjustment = adjustment;
        Width = width;
        Height = height;
        Mode = mode;
    }

    /// <summary>Gets the edge to move.</summary>
    public ResizeDirection? Direction { get; }

    /// <summary>Gets how many cells to move the edge by.</summary>
    public int? Adjustment { get; }

    /// <summary>Gets the explicit width.</summary>
    public int? Width { get; }

    /// <summary>Gets the explicit height.</summary>
    public int? Height { get; }

    /// <summary>Gets the sizing to follow against the window's clients.</summary>
    public WindowResizeMode? Mode { get; }

    private static void ThrowIfNotPositive(int? value, string parameterName)
    {
        if (value is int cells && cells <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, cells, "Cells must be positive.");
        }
    }
}
