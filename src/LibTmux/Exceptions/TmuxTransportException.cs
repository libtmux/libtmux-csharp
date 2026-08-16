using System.Collections.ObjectModel;

namespace LibTmux;

/// <summary>Reports a process-transport failure.</summary>
public sealed class TmuxTransportException : LibTmuxException
{
    private readonly ReadOnlyCollection<string> _arguments;

    /// <summary>Initializes a transport exception whose dispatch state is unknown.</summary>
    public TmuxTransportException(
        string message,
        IReadOnlyList<string> arguments,
        Exception? innerException = null)
        : this(message, arguments, TmuxDispatchState.Unknown, innerException)
    {
    }

    /// <summary>Initializes a transport exception that knows whether tmux was started.</summary>
    /// <remarks>
    /// A transport failure is the one place the answer differs by cause. Rejecting
    /// a command for being too long, or failing to start the client at all, happens
    /// before tmux exists and is
    /// <see cref="TmuxDispatchState.NotDispatched"/>. A client that started and then
    /// died is <see cref="TmuxDispatchState.Unknown"/>, because tmux may well have
    /// acted before the pipe broke.
    /// </remarks>
    public TmuxTransportException(
        string message,
        IReadOnlyList<string> arguments,
        TmuxDispatchState dispatch,
        Exception? innerException = null)
        : base(message, dispatch, innerException)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _arguments = Array.AsReadOnly(arguments.ToArray());
    }

    /// <summary>Gets the logical tmux arguments.</summary>
    public IReadOnlyList<string> Arguments => _arguments;
}
