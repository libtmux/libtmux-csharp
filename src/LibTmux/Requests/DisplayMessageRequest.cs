namespace LibTmux;

/// <summary>Describes one <c>display-message</c> invocation.</summary>
public sealed record DisplayMessageRequest
{
    /// <summary>Initializes a display-message request.</summary>
    /// <param name="message">The message, which tmux expands as a format.</param>
    /// <param name="returnText">Whether the message is printed rather than shown.</param>
    /// <param name="format">A format string used in place of the message.</param>
    /// <param name="allFormats">Whether every format variable is listed.</param>
    /// <param name="verbose">Whether format expansion is reported.</param>
    /// <param name="noExpand">Whether the message is sent without format expansion.</param>
    /// <param name="targetClient">The client to show the message on.</param>
    /// <param name="delay">How long the message stays up.</param>
    /// <param name="notify">Whether the message is delivered as a notification.</param>
    /// <param name="updatePane">Whether the pane is redrawn while shown.</param>
    public DisplayMessageRequest(
        string message = "",
        bool returnText = false,
        string? format = null,
        bool allFormats = false,
        bool verbose = false,
        bool noExpand = false,
        string? targetClient = null,
        TimeSpan? delay = null,
        bool notify = false,
        bool updatePane = false)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (delay is TimeSpan window && window < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delay),
                window,
                "A delay cannot be negative.");
        }

        Message = message;
        ReturnText = returnText;
        Format = format;
        AllFormats = allFormats;
        Verbose = verbose;
        NoExpand = noExpand;
        TargetClient = targetClient;
        Delay = delay;
        Notify = notify;
        UpdatePane = updatePane;
    }

    /// <summary>Gets the message, which tmux expands as a format.</summary>
    public string Message { get; }

    /// <summary>Gets whether the message is printed rather than shown.</summary>
    public bool ReturnText { get; }

    /// <summary>Gets the format string used in place of the message.</summary>
    public string? Format { get; }

    /// <summary>Gets whether every format variable is listed.</summary>
    public bool AllFormats { get; }

    /// <summary>Gets whether format expansion is reported.</summary>
    public bool Verbose { get; }

    /// <summary>Gets whether the message is sent without format expansion.</summary>
    /// <remarks>tmux gained this in 3.4; older servers always expand.</remarks>
    public bool NoExpand { get; }

    /// <summary>Gets the client to show the message on.</summary>
    public string? TargetClient { get; }

    /// <summary>Gets how long the message stays up.</summary>
    public TimeSpan? Delay { get; }

    /// <summary>Gets whether the message is delivered as a notification.</summary>
    public bool Notify { get; }

    /// <summary>Gets whether the pane is redrawn while the message is shown.</summary>
    /// <remarks>Only a pane can honour this; a window-scoped call rejects it.</remarks>
    public bool UpdatePane { get; }
}
