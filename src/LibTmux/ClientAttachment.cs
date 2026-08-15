namespace LibTmux;

/// <summary>What one client is looking at.</summary>
/// <remarks>
/// The three come from one reading, so they agree with each other. Asking for
/// them separately would let the client move between the answers.
/// </remarks>
/// <param name="Session">The session the client is attached to.</param>
/// <param name="Window">The window that session is showing.</param>
/// <param name="Pane">The pane that window has active.</param>
public sealed record ClientAttachment(Session? Session, Window? Window, Pane? Pane);
