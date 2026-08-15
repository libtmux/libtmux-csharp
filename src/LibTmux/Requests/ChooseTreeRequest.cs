namespace LibTmux;

/// <summary>Names how a chooser orders its rows.</summary>
public enum ChooseTreeSort
{
    /// <summary>Order by index.</summary>
    Index = 0,

    /// <summary>Order by name.</summary>
    Name = 1,

    /// <summary>Order by activity time.</summary>
    /// <remarks>tmux dropped this in 3.7 and rejects it there.</remarks>
    Time = 2,

    /// <summary>Order by size.</summary>
    /// <remarks>Accepted on every supported tmux, but only sorts from 3.7.</remarks>
    Size = 3,
}

/// <summary>Describes one <c>choose-tree</c> invocation.</summary>
public sealed record ChooseTreeRequest
{
    /// <summary>Initializes a tree-chooser request.</summary>
    /// <param name="sessionsCollapsed">Whether sessions start collapsed.</param>
    /// <param name="windowsCollapsed">Whether windows start collapsed.</param>
    /// <param name="format">The format each row renders with.</param>
    /// <param name="nativeFilter">A raw tmux filter limiting the rows.</param>
    /// <param name="sort">How the rows are ordered.</param>
    /// <param name="reverse">Whether the order is reversed.</param>
    /// <param name="zoom">Whether the chooser pane is zoomed.</param>
    public ChooseTreeRequest(
        bool sessionsCollapsed = false,
        bool windowsCollapsed = false,
        string? format = null,
        UnsafeTmuxFilter? nativeFilter = null,
        ChooseTreeSort? sort = null,
        bool reverse = false,
        bool zoom = false)
    {
        if (sort is not null && !Enum.IsDefined(sort.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(sort));
        }

        SessionsCollapsed = sessionsCollapsed;
        WindowsCollapsed = windowsCollapsed;
        Format = format;
        NativeFilter = nativeFilter;
        Sort = sort;
        Reverse = reverse;
        Zoom = zoom;
    }

    /// <summary>Gets whether sessions start collapsed.</summary>
    public bool SessionsCollapsed { get; }

    /// <summary>Gets whether windows start collapsed.</summary>
    public bool WindowsCollapsed { get; }

    /// <summary>Gets the format each row renders with.</summary>
    public string? Format { get; }

    /// <summary>Gets the raw tmux filter limiting the rows.</summary>
    public UnsafeTmuxFilter? NativeFilter { get; }

    /// <summary>Gets how the rows are ordered.</summary>
    public ChooseTreeSort? Sort { get; }

    /// <summary>Gets whether the order is reversed.</summary>
    public bool Reverse { get; }

    /// <summary>Gets whether the chooser pane is zoomed.</summary>
    public bool Zoom { get; }
}
