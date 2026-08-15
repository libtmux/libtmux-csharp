namespace LibTmux;

/// <summary>Defines tmux option scopes.</summary>
public enum OptionScope
{
    /// <summary>Uses server scope.</summary>
    Server = 0,

    /// <summary>Uses session scope.</summary>
    Session = 1,

    /// <summary>Uses window scope.</summary>
    Window = 2,

    /// <summary>Uses pane scope.</summary>
    Pane = 3,
}

/// <summary>Defines pane placement directions.</summary>
public enum PaneDirection
{
    /// <summary>Places the pane above.</summary>
    Above = 0,

    /// <summary>Places the pane below.</summary>
    Below = 1,

    /// <summary>Places the pane to the left.</summary>
    Left = 2,

    /// <summary>Places the pane to the right.</summary>
    Right = 3,
}

/// <summary>Defines pane resize directions.</summary>
public enum ResizeDirection
{
    /// <summary>Resizes upward.</summary>
    Up = 0,

    /// <summary>Resizes downward.</summary>
    Down = 1,

    /// <summary>Resizes leftward.</summary>
    Left = 2,

    /// <summary>Resizes rightward.</summary>
    Right = 3,
}

/// <summary>Defines relative window placement.</summary>
public enum WindowDirection
{
    /// <summary>Places the window before the target.</summary>
    Before = 0,

    /// <summary>Places the window after the target.</summary>
    After = 1,
}
