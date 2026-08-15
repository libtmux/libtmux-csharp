namespace LibTmux;

/// <summary>Describes one <c>copy-mode</c> invocation.</summary>
public sealed record CopyModeRequest
{
    /// <summary>Initializes a copy-mode request.</summary>
    /// <param name="scrollUp">Whether the pane scrolls up one page on entry.</param>
    /// <param name="exitOnBottom">Whether reaching the bottom leaves copy mode.</param>
    /// <param name="mouseDrag">Whether the mode is entered for a mouse drag.</param>
    /// <param name="cancel">Whether copy mode is left instead of entered.</param>
    /// <param name="pageDown">Whether the pane scrolls down one page on entry.</param>
    /// <param name="sourcePane">A pane whose content is shown instead.</param>
    public CopyModeRequest(
        bool scrollUp = false,
        bool exitOnBottom = false,
        bool mouseDrag = false,
        bool cancel = false,
        bool pageDown = false,
        string? sourcePane = null)
    {
        ScrollUp = scrollUp;
        ExitOnBottom = exitOnBottom;
        MouseDrag = mouseDrag;
        Cancel = cancel;
        PageDown = pageDown;
        SourcePane = sourcePane;
    }

    /// <summary>Gets whether the pane scrolls up one page on entry.</summary>
    public bool ScrollUp { get; }

    /// <summary>Gets whether reaching the bottom leaves copy mode.</summary>
    public bool ExitOnBottom { get; }

    /// <summary>Gets whether the mode is entered for a mouse drag.</summary>
    /// <remarks>
    /// Without a real mouse event tmux accepts this and enters no mode.
    /// </remarks>
    public bool MouseDrag { get; }

    /// <summary>Gets whether copy mode is left instead of entered.</summary>
    public bool Cancel { get; }

    /// <summary>Gets whether the pane scrolls down one page on entry.</summary>
    /// <remarks>tmux gained this in 3.5.</remarks>
    public bool PageDown { get; }

    /// <summary>Gets the pane whose content is shown instead.</summary>
    public string? SourcePane { get; }
}
