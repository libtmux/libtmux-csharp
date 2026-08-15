using System.Collections.ObjectModel;

namespace LibTmux;

/// <summary>Reports a process-transport failure.</summary>
public sealed class TmuxTransportException : LibTmuxException
{
    private readonly ReadOnlyCollection<string> _arguments;

    /// <summary>Initializes a transport exception.</summary>
    public TmuxTransportException(
        string message,
        IReadOnlyList<string> arguments,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _arguments = Array.AsReadOnly(arguments.ToArray());
    }

    /// <summary>Gets the logical tmux arguments.</summary>
    public IReadOnlyList<string> Arguments => _arguments;
}
