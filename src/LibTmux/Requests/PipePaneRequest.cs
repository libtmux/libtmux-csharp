namespace LibTmux;

/// <summary>Describes one <c>pipe-pane</c> invocation.</summary>
public sealed record PipePaneRequest
{
    /// <summary>Initializes a pane-piping request.</summary>
    /// <param name="command">The command to pipe through, or null to stop piping.</param>
    /// <param name="outputOnly">Whether only pane output is piped.</param>
    /// <param name="inputOnly">Whether only pane input is piped.</param>
    /// <param name="toggle">Whether an identical existing pipe is stopped instead.</param>
    public PipePaneRequest(
        string? command = null,
        bool outputOnly = false,
        bool inputOnly = false,
        bool toggle = false)
    {
        Command = command;
        OutputOnly = outputOnly;
        InputOnly = inputOnly;
        Toggle = toggle;
    }

    /// <summary>Gets the command to pipe through, or null to stop piping.</summary>
    /// <remarks>
    /// Omitting a command does not leave an existing pipe alone: it stops it.
    /// </remarks>
    public string? Command { get; }

    /// <summary>Gets whether only pane output is piped.</summary>
    public bool OutputOnly { get; }

    /// <summary>Gets whether only pane input is piped.</summary>
    public bool InputOnly { get; }

    /// <summary>Gets whether an identical existing pipe is stopped instead.</summary>
    public bool Toggle { get; }
}
