namespace LibTmux;

/// <summary>Reports a command-policy failure.</summary>
public sealed class TmuxCommandException : LibTmuxException
{
    // This exception carries a result, and a result only exists because tmux
    // ran and answered. The failure is tmux refusing, not the command going
    // missing, so the dispatch state is not a parameter -- it is a fact about
    // the type.

    /// <summary>Initializes a command exception.</summary>
    public TmuxCommandException(
        string message,
        TmuxCommandResult result,
        Exception? innerException = null)
        : base(message, TmuxDispatchState.Dispatched, innerException)
        => Result = result ?? throw new ArgumentNullException(nameof(result));

    /// <summary>Gets the inspectable command result.</summary>
    public TmuxCommandResult Result { get; }
}
