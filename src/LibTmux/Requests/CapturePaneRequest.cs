namespace LibTmux;

/// <summary>Names one end of a capture range.</summary>
/// <remarks>
/// tmux writes the extremes as a literal <c>-</c> rather than a number, which
/// means the beginning of the history for a start and the end of the visible
/// pane for an end. The two named values carry that same absent line number and
/// differ only in which flag they read well against.
/// </remarks>
public readonly record struct CapturePanePosition
{
    /// <summary>Initializes a position at one line.</summary>
    /// <param name="lineNumber">The line, where zero is the top of the visible pane.</param>
    public CapturePanePosition(int lineNumber) => LineNumber = lineNumber;

    /// <summary>Gets the oldest line tmux still holds.</summary>
    public static CapturePanePosition BeginningOfHistory => default;

    /// <summary>Gets the last line of the visible pane.</summary>
    public static CapturePanePosition EndOfVisiblePane => default;

    /// <summary>Gets the line, or null for the extreme tmux writes as <c>-</c>.</summary>
    public int? LineNumber { get; }
}

/// <summary>Describes one <c>capture-pane</c> invocation.</summary>
public sealed record CapturePaneRequest
{
    /// <summary>Initializes a capture request.</summary>
    /// <param name="startLine">The first line to capture.</param>
    /// <param name="endLine">The last line to capture.</param>
    /// <param name="escapeSequences">Whether escape sequences are preserved.</param>
    /// <param name="escapeNonPrintable">Whether unprintable bytes are escaped as octal.</param>
    /// <param name="joinWrappedLines">Whether wrapped lines are joined.</param>
    /// <param name="preserveTrailingSpaces">Whether trailing spaces are kept.</param>
    /// <param name="trimTrailingSpaces">Whether trailing spaces are removed.</param>
    /// <param name="alternateScreen">Whether the alternate screen is captured.</param>
    /// <param name="quiet">Whether a missing alternate screen is not an error.</param>
    /// <param name="modeScreen">Whether the pane's mode screen is captured.</param>
    /// <param name="pending">Whether pending output is captured.</param>
    /// <param name="hyperlinks">Whether hyperlinks are captured.</param>
    /// <param name="lineNumbers">Whether each line carries its number.</param>
    /// <param name="lineFlags">Whether each line carries its flags.</param>
    public CapturePaneRequest(
        CapturePanePosition? startLine = null,
        CapturePanePosition? endLine = null,
        bool escapeSequences = false,
        bool escapeNonPrintable = false,
        bool joinWrappedLines = false,
        bool preserveTrailingSpaces = false,
        bool trimTrailingSpaces = false,
        bool alternateScreen = false,
        bool quiet = false,
        bool modeScreen = false,
        bool pending = false,
        bool hyperlinks = false,
        bool lineNumbers = false,
        bool lineFlags = false)
    {
        StartLine = startLine;
        EndLine = endLine;
        EscapeSequences = escapeSequences;
        EscapeNonPrintable = escapeNonPrintable;
        JoinWrappedLines = joinWrappedLines;
        PreserveTrailingSpaces = preserveTrailingSpaces;
        TrimTrailingSpaces = trimTrailingSpaces;
        AlternateScreen = alternateScreen;
        Quiet = quiet;
        ModeScreen = modeScreen;
        Pending = pending;
        Hyperlinks = hyperlinks;
        LineNumbers = lineNumbers;
        LineFlags = lineFlags;
    }

    /// <summary>Gets the first line to capture.</summary>
    public CapturePanePosition? StartLine { get; }

    /// <summary>Gets the last line to capture.</summary>
    public CapturePanePosition? EndLine { get; }

    /// <summary>Gets whether escape sequences are preserved.</summary>
    public bool EscapeSequences { get; }

    /// <summary>Gets whether unprintable bytes are escaped as octal.</summary>
    public bool EscapeNonPrintable { get; }

    /// <summary>Gets whether wrapped lines are joined.</summary>
    /// <remarks>tmux applies this in place of trailing-space handling.</remarks>
    public bool JoinWrappedLines { get; }

    /// <summary>Gets whether trailing spaces are kept.</summary>
    public bool PreserveTrailingSpaces { get; }

    /// <summary>Gets whether trailing spaces are removed.</summary>
    public bool TrimTrailingSpaces { get; }

    /// <summary>Gets whether the alternate screen is captured.</summary>
    /// <remarks>
    /// A pane with no alternate screen makes this an error unless
    /// <see cref="Quiet" /> is set.
    /// </remarks>
    public bool AlternateScreen { get; }

    /// <summary>Gets whether a missing alternate screen is not an error.</summary>
    /// <remarks>This does not quieten a target that cannot be resolved.</remarks>
    public bool Quiet { get; }

    /// <summary>Gets whether the pane's mode screen is captured.</summary>
    public bool ModeScreen { get; }

    /// <summary>Gets whether pending output is captured.</summary>
    public bool Pending { get; }

    /// <summary>Gets whether hyperlinks are captured.</summary>
    public bool Hyperlinks { get; }

    /// <summary>Gets whether each line carries its number.</summary>
    public bool LineNumbers { get; }

    /// <summary>Gets whether each line carries its flags.</summary>
    public bool LineFlags { get; }
}
