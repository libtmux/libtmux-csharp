namespace LibTmux;

/// <summary>Describes one <c>find-window</c> invocation.</summary>
public sealed record FindWindowRequest
{
    /// <summary>Initializes a window-search request.</summary>
    /// <param name="pattern">The text to look for.</param>
    /// <param name="matchContent">Whether pane content is searched.</param>
    /// <param name="ignoreCase">Whether the search ignores case.</param>
    /// <param name="matchName">Whether window names are searched.</param>
    /// <param name="regex">Whether the pattern is a regular expression.</param>
    /// <param name="matchTitle">Whether pane titles are searched.</param>
    /// <exception cref="ArgumentException"><paramref name="pattern" /> is blank.</exception>
    public FindWindowRequest(
        string pattern,
        bool matchContent = false,
        bool ignoreCase = false,
        bool matchName = false,
        bool regex = false,
        bool matchTitle = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        Pattern = pattern;
        MatchContent = matchContent;
        IgnoreCase = ignoreCase;
        MatchName = matchName;
        Regex = regex;
        MatchTitle = matchTitle;
    }

    /// <summary>Gets the text to look for.</summary>
    public string Pattern { get; }

    /// <summary>Gets whether pane content is searched.</summary>
    public bool MatchContent { get; }

    /// <summary>Gets whether the search ignores case.</summary>
    public bool IgnoreCase { get; }

    /// <summary>Gets whether window names are searched.</summary>
    public bool MatchName { get; }

    /// <summary>Gets whether the pattern is a regular expression.</summary>
    public bool Regex { get; }

    /// <summary>Gets whether pane titles are searched.</summary>
    public bool MatchTitle { get; }
}
