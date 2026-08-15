namespace LibTmux;

/// <summary>Describes one <c>attach-session</c> invocation.</summary>
public sealed record AttachSessionRequest
{
    private readonly string[]? _clientFlags;

    /// <summary>Initializes a session-attachment request.</summary>
    /// <param name="target">The session to attach, or null for the caller's own.</param>
    /// <param name="detachOthers">Whether other clients are detached.</param>
    /// <param name="readOnly">Whether the client attaches read-only.</param>
    /// <param name="exitOnDetach">Whether the client exits when the session is destroyed.</param>
    /// <param name="clientFlags">Client flags sent as one comma-separated <c>-f</c> value.</param>
    public AttachSessionRequest(
        string? target = null,
        bool detachOthers = false,
        bool readOnly = false,
        bool exitOnDetach = false,
        IReadOnlyList<string>? clientFlags = null)
    {
        Target = target;
        DetachOthers = detachOthers;
        ReadOnly = readOnly;
        ExitOnDetach = exitOnDetach;
        // The request is read again at dispatch, so a caller that kept the list
        // could otherwise change the argv after constructing it.
        _clientFlags = clientFlags is null ? null : [.. clientFlags];
    }

    /// <summary>Gets the session to attach, or null for the caller's own.</summary>
    public string? Target { get; }

    /// <summary>Gets whether other clients are detached.</summary>
    public bool DetachOthers { get; }

    /// <summary>Gets whether the client attaches read-only.</summary>
    public bool ReadOnly { get; }

    /// <summary>Gets whether the client exits when the session is destroyed.</summary>
    public bool ExitOnDetach { get; }

    /// <summary>Gets the client flags sent with <c>-f</c>.</summary>
    public IReadOnlyList<string>? ClientFlags => _clientFlags;
}
