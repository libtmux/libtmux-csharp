namespace LibTmux;

/// <summary>Reports an expired bounded wait.</summary>
public sealed class TmuxWaitTimeoutException : TimeoutException
{
    /// <summary>Initializes a wait-timeout exception.</summary>
    public TmuxWaitTimeoutException(
        string message,
        TimeSpan timeout,
        Exception? innerException = null)
        : base(message, innerException)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "The timeout must be positive.");
        }

        Timeout = timeout;
    }

    /// <summary>Gets the expired timeout.</summary>
    public TimeSpan Timeout { get; }
}
