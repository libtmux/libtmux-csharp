namespace LibTmux;

/// <summary>Reports a command-policy failure.</summary>
public sealed class TmuxCommandException : LibTmuxException
{
    // A result exists only because tmux ran, so this exception is always
    // TmuxDispatchState.Dispatched -- not a constructor parameter.

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
