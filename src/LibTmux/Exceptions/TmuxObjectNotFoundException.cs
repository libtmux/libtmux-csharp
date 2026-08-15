namespace LibTmux;

/// <summary>Reports a missing tmux object.</summary>
public sealed class TmuxObjectNotFoundException : LibTmuxException
{
    /// <summary>Initializes a missing-object exception.</summary>
    public TmuxObjectNotFoundException(
        string message,
        string target,
        Exception? innerException = null)
        : base(message, innerException)
        => Target = target ?? throw new ArgumentNullException(nameof(target));

    /// <summary>Gets the missing tmux target.</summary>
    public string Target { get; }
}
