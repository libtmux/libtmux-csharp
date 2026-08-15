namespace LibTmux;

/// <summary>Reports a command-policy failure.</summary>
public sealed class TmuxCommandException : LibTmuxException
{
    /// <summary>Initializes a command exception.</summary>
    public TmuxCommandException(
        string message,
        TmuxCommandResult result,
        Exception? innerException = null)
        : base(message, innerException)
        => Result = result ?? throw new ArgumentNullException(nameof(result));

    /// <summary>Gets the inspectable command result.</summary>
    public TmuxCommandResult Result { get; }
}
