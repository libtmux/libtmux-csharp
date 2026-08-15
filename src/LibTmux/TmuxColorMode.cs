namespace LibTmux;

/// <summary>Defines the tmux client color mode.</summary>
public enum TmuxColorMode
{
    /// <summary>Uses tmux's default color behavior.</summary>
    Default = 0,

    /// <summary>Requests 256-color mode.</summary>
    Colors256 = 2,

    /// <summary>Requests RGB true-color mode.</summary>
    TrueColor = 3,
}
