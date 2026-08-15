namespace LibTmux;

/// <summary>Reports a failure to clean up a canceled tmux client.</summary>
public sealed class TmuxCleanupException : LibTmuxException
{
    /// <summary>Initializes a cleanup exception.</summary>
    public TmuxCleanupException(
        string message,
        OperationCanceledException originalCancellation,
        int clientProcessId,
        Exception cleanupFailure)
        : base(message, cleanupFailure)
    {
        OriginalCancellation = originalCancellation
            ?? throw new ArgumentNullException(nameof(originalCancellation));
        if (clientProcessId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(clientProcessId),
                clientProcessId,
                "The client process identifier must be positive.");
        }

        ClientProcessId = clientProcessId;
        CleanupFailure = cleanupFailure ?? throw new ArgumentNullException(nameof(cleanupFailure));
    }

    /// <summary>Gets the original cancellation.</summary>
    public OperationCanceledException OriginalCancellation { get; }

    /// <summary>Gets the disposable client process identifier.</summary>
    public int ClientProcessId { get; }

    /// <summary>Gets the cleanup failure.</summary>
    public Exception CleanupFailure { get; }
}
