namespace LibTmux;

/// <summary>Describes one <c>send-keys</c> invocation.</summary>
public sealed record SendKeysRequest
{
    /// <summary>Initializes a key-sending request.</summary>
    /// <param name="text">The text or key names to send.</param>
    /// <param name="enter">Whether Enter follows the text.</param>
    /// <param name="suppressHistory">Whether the shell is asked not to record the line.</param>
    /// <param name="literal">Whether the text is sent verbatim rather than as key names.</param>
    /// <param name="reset">Whether the pane's terminal state is reset first.</param>
    /// <param name="copyModeCommand">A copy-mode command to send instead of text.</param>
    /// <param name="repeat">How many times the keys repeat.</param>
    /// <param name="expandFormats">Whether the text is expanded as a format.</param>
    /// <param name="hexKeys">Whether key names are read as hexadecimal.</param>
    /// <param name="targetClient">The client whose keys are sent.</param>
    /// <param name="keyName">Whether the text names a key rather than a string.</param>
    public SendKeysRequest(
        string? text = null,
        bool enter = true,
        bool suppressHistory = false,
        bool literal = false,
        bool reset = false,
        string? copyModeCommand = null,
        int? repeat = null,
        bool expandFormats = false,
        bool hexKeys = false,
        string? targetClient = null,
        bool keyName = false)
    {
        Text = text;
        Enter = enter;
        SuppressHistory = suppressHistory;
        Literal = literal;
        Reset = reset;
        CopyModeCommand = copyModeCommand;
        Repeat = repeat;
        ExpandFormats = expandFormats;
        HexKeys = hexKeys;
        TargetClient = targetClient;
        KeyName = keyName;
    }

    /// <summary>Gets the text or key names to send.</summary>
    public string? Text { get; }

    /// <summary>Gets whether Enter follows the text.</summary>
    /// <remarks>
    /// Enter is a second command rather than an appended key, because a literal
    /// send would otherwise type the five characters of its name.
    /// </remarks>
    public bool Enter { get; }

    /// <summary>Gets whether the shell is asked not to record the line.</summary>
    /// <remarks>
    /// There is no tmux flag for this: the text is sent with a leading space,
    /// which most shells take as a signal to keep it out of history.
    /// </remarks>
    public bool SuppressHistory { get; }

    /// <summary>Gets whether the text is sent verbatim rather than as key names.</summary>
    public bool Literal { get; }

    /// <summary>Gets whether the pane's terminal state is reset first.</summary>
    public bool Reset { get; }

    /// <summary>Gets the copy-mode command to send instead of text.</summary>
    public string? CopyModeCommand { get; }

    /// <summary>Gets how many times the keys repeat.</summary>
    public int? Repeat { get; }

    /// <summary>Gets whether the text is expanded as a format.</summary>
    public bool ExpandFormats { get; }

    /// <summary>Gets whether key names are read as hexadecimal.</summary>
    public bool HexKeys { get; }

    /// <summary>Gets the client whose keys are sent.</summary>
    /// <remarks>tmux gained this in 3.4.</remarks>
    public string? TargetClient { get; }

    /// <summary>Gets whether the text names a key rather than a string.</summary>
    /// <remarks>tmux gained this in 3.4.</remarks>
    public bool KeyName { get; }
}
