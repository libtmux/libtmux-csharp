namespace LibTmux;

/// <summary>Describes one <c>move-window</c> invocation.</summary>
public sealed record MoveWindowRequest
{
    /// <summary>Initializes a window-move request.</summary>
    /// <param name="destination">The window part of the target, empty for the next free index.</param>
    /// <param name="session">The destination session, or null for the window's own.</param>
    /// <param name="direction">Whether to insert before or after the destination.</param>
    /// <param name="noSelect">Whether the moved window is left unselected.</param>
    /// <param name="replaceExisting">Whether a window already at the index is replaced.</param>
    /// <param name="renumber">Whether the destination session's windows are renumbered.</param>
    public MoveWindowRequest(
        string destination = "",
        string? session = null,
        WindowDirection? direction = null,
        bool noSelect = false,
        bool replaceExisting = false,
        bool renumber = false)
    {
        Destination = destination;
        Session = session;
        Direction = direction;
        NoSelect = noSelect;
        ReplaceExisting = replaceExisting;
        Renumber = renumber;
    }

    /// <summary>Gets the window part of the target, empty for the next free index.</summary>
    public string Destination { get; }

    /// <summary>Gets the destination session, or null for the window's own.</summary>
    public string? Session { get; }

    /// <summary>Gets whether to insert before or after the destination.</summary>
    public WindowDirection? Direction { get; }

    /// <summary>Gets whether the moved window is left unselected.</summary>
    public bool NoSelect { get; }

    /// <summary>Gets whether a window already at the index is replaced.</summary>
    public bool ReplaceExisting { get; }

    /// <summary>Gets whether the destination session's windows are renumbered.</summary>
    /// <remarks>
    /// tmux renumbers and returns without moving anything, ignoring every other
    /// flag on the request, so this is a renumber request rather than a move
    /// that also renumbers.
    /// </remarks>
    public bool Renumber { get; }
}
