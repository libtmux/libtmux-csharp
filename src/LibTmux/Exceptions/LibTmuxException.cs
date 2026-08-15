namespace LibTmux;

/// <summary>Provides the base exception for remote tmux failures.</summary>
public class LibTmuxException : Exception
{
    /// <summary>Initializes a LibTmux exception.</summary>
    public LibTmuxException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
