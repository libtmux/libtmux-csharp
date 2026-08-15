namespace LibTmux;

/// <summary>Describes one <c>paste-buffer</c> invocation.</summary>
public sealed record PasteBufferRequest
{
    /// <summary>Initializes a buffer-paste request.</summary>
    /// <param name="name">The buffer to paste, or null for the most recent.</param>
    /// <param name="deleteAfter">Whether the buffer is deleted once pasted.</param>
    /// <param name="useLineFeedSeparator">Whether line feeds separate the lines.</param>
    /// <param name="bracketed">Whether the paste is bracketed.</param>
    /// <param name="separator">A separator used between lines.</param>
    /// <param name="rawBytes">Whether the bytes are pasted without translation.</param>
    public PasteBufferRequest(
        string? name = null,
        bool deleteAfter = false,
        bool useLineFeedSeparator = false,
        bool bracketed = false,
        string? separator = null,
        bool rawBytes = false)
    {
        Name = name;
        DeleteAfter = deleteAfter;
        UseLineFeedSeparator = useLineFeedSeparator;
        Bracketed = bracketed;
        Separator = separator;
        RawBytes = rawBytes;
    }

    /// <summary>Gets the buffer to paste, or null for the most recent.</summary>
    public string? Name { get; }

    /// <summary>Gets whether the buffer is deleted once pasted.</summary>
    public bool DeleteAfter { get; }

    /// <summary>Gets whether line feeds separate the lines.</summary>
    public bool UseLineFeedSeparator { get; }

    /// <summary>Gets whether the paste is bracketed.</summary>
    public bool Bracketed { get; }

    /// <summary>Gets the separator used between lines.</summary>
    public string? Separator { get; }

    /// <summary>Gets whether the bytes are pasted without translation.</summary>
    /// <remarks>
    /// tmux gained the flag in 3.7. Older servers already paste raw bytes, so
    /// omitting it there asks for what they already do.
    /// </remarks>
    public bool RawBytes { get; }
}
