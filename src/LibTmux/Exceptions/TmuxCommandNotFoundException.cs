namespace LibTmux;

/// <summary>Reports a missing tmux executable.</summary>
/// <remarks>
/// Nothing ran, so this is always <see cref="TmuxDispatchState.NotDispatched"/>:
/// fixing the path and trying again repeats no side effect.
/// </remarks>
public sealed class TmuxCommandNotFoundException : LibTmuxException
{
    /// <summary>Initializes a command-not-found exception.</summary>
    public TmuxCommandNotFoundException(
        string message,
        string tmuxBinaryPath,
        Exception? innerException = null)
        : base(message, TmuxDispatchState.NotDispatched, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tmuxBinaryPath);
        TmuxBinaryPath = tmuxBinaryPath;
    }

    /// <summary>Gets the configured tmux executable path.</summary>
    public string TmuxBinaryPath { get; }
}
