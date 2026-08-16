namespace LibTmux;

/// <summary>Says whether a failed command reached tmux, which is what decides if retrying is safe.</summary>
/// <remarks>
/// Retrying is the obvious response to a failure and it is not always sound.
/// A command that never reached tmux can be sent again and nothing has
/// happened twice. A command tmux already ran has already done whatever it
/// does, and <c>kill-session</c> or <c>send-keys</c> run twice is not the same
/// as run once.
///
/// So a caller needs to distinguish the two, and the honest answer is
/// sometimes neither: a client that died mid-command leaves no way to know
/// whether tmux acted on it. That third state is <see cref="Unknown"/>, and it
/// is the default, because assuming a command did not run is the assumption
/// that repeats side effects.
/// </remarks>
public enum TmuxDispatchState
{
    /// <summary>
    /// Whether tmux acted on the command cannot be determined. Treat a retry as
    /// capable of repeating whatever the command does.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The command never reached tmux, so nothing was done and a retry repeats
    /// nothing. This is the only state in which retrying is unconditionally safe.
    /// </summary>
    NotDispatched = 1,

    /// <summary>
    /// tmux ran the command and answered. The failure is tmux refusing or
    /// reporting an error, not the command going missing, so any side effect it
    /// had before failing has already happened.
    /// </summary>
    Dispatched = 2,
}
