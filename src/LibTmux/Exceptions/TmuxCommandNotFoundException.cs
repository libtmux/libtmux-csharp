namespace LibTmux;

/// <summary>Reports a missing tmux executable.</summary>
public sealed class TmuxCommandNotFoundException : LibTmuxException
{
    /// <summary>Initializes a command-not-found exception.</summary>
    public TmuxCommandNotFoundException(
        string message,
        string tmuxBinaryPath,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tmuxBinaryPath);
        TmuxBinaryPath = tmuxBinaryPath;
    }

    /// <summary>Gets the configured tmux executable path.</summary>
    public string TmuxBinaryPath { get; }
}
