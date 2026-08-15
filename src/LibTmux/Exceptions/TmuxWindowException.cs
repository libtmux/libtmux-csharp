namespace LibTmux;

/// <summary>Thrown when a window operation is refused before tmux sees it.</summary>
/// <remarks>
/// Some window requests cannot safely be handed to tmux to reject. tmux 3.3a
/// crashes its whole server on an unrecognised layout name, taking every
/// session on the socket with it, so a bad layout is refused here instead.
/// </remarks>
public sealed class TmuxWindowException : LibTmuxException
{
    /// <summary>Initializes the exception for one window.</summary>
    /// <param name="message">What was refused.</param>
    /// <param name="windowId">The window the request named.</param>
    /// <param name="innerException">The underlying failure, when any.</param>
    public TmuxWindowException(
        string message,
        WindowId windowId,
        Exception? innerException = null)
        : base(message, innerException) => WindowId = windowId;

    /// <summary>Gets the window the request named.</summary>
    public WindowId WindowId { get; }
}
