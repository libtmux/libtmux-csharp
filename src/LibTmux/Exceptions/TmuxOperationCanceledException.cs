using System.Diagnostics.CodeAnalysis;

namespace LibTmux;

/// <summary>Reports cancellation after a tmux client started.</summary>
public sealed class TmuxOperationCanceledException : OperationCanceledException
{
    /// <summary>Initializes a tmux cancellation exception.</summary>
    [SuppressMessage(
        "Design",
        "CA1068:CancellationToken parameters must come last",
        Justification = "The reviewed public constructor keeps cancellation state adjacent to its message.")]
    public TmuxOperationCanceledException(
        string message,
        CancellationToken cancellationToken,
        bool commandMayHaveExecuted,
        int clientProcessId,
        Exception? innerException = null)
        : base(message, innerException, cancellationToken)
    {
        if (clientProcessId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(clientProcessId),
                clientProcessId,
                "The client process identifier must be positive.");
        }

        CommandMayHaveExecuted = commandMayHaveExecuted;
        ClientProcessId = clientProcessId;
    }

    /// <summary>Gets whether tmux may have observed the command.</summary>
    public bool CommandMayHaveExecuted { get; }

    /// <summary>Gets the disposable client process identifier.</summary>
    public int ClientProcessId { get; }
}
