namespace LibTmux;

/// <summary>Names a layout change that needs no layout string.</summary>
public enum SelectLayoutMode
{
    /// <summary>Spread the panes out evenly.</summary>
    Spread = 0,

    /// <summary>Move to the next layout.</summary>
    Next = 1,

    /// <summary>Move to the previous layout.</summary>
    Previous = 2,
}

/// <summary>Describes one <c>select-layout</c> invocation.</summary>
public sealed record SelectLayoutRequest
{
    /// <summary>Initializes a layout-selection request.</summary>
    /// <param name="layout">A named layout, or a layout string tmux dumped.</param>
    /// <param name="mode">A layout change that needs no name.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode" /> is undefined.</exception>
    public SelectLayoutRequest(string? layout = null, SelectLayoutMode? mode = null)
    {
        if (mode is not null && !Enum.IsDefined(mode.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        Layout = layout;
        Mode = mode;
    }

    /// <summary>Gets the named layout, or a layout string tmux dumped.</summary>
    public string? Layout { get; }

    /// <summary>Gets the layout change that needs no name.</summary>
    public SelectLayoutMode? Mode { get; }
}
