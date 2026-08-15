namespace LibTmux;

/// <summary>Thrown when a session name is already taken.</summary>
/// <remarks>
/// Distinct from a generic command failure because a caller can act on it:
/// pick another name, or ask tmux to replace the existing session.
/// </remarks>
public sealed class TmuxSessionExistsException : LibTmuxException
{
    /// <summary>Initializes the exception for one taken session name.</summary>
    /// <param name="message">What tmux reported.</param>
    /// <param name="sessionName">The name already in use.</param>
    /// <param name="innerException">The underlying failure, when any.</param>
    public TmuxSessionExistsException(
        string message,
        string sessionName,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionName);
        SessionName = sessionName;
    }

    /// <summary>Gets the session name that is already in use.</summary>
    public string SessionName { get; }
}
