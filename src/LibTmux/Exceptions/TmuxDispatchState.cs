namespace LibTmux;

/// <summary>Says whether a failed command reached tmux, which is what decides if retrying is safe.</summary>
/// <remarks>
/// Retrying is safe only when the command never reached tmux.
/// <see cref="Unknown"/> is the default because assuming otherwise repeats
/// a side effect tmux already ran.
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
