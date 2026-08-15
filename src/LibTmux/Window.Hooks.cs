using System.Runtime.Versioning;

namespace LibTmux;

/// <summary>Reaches this window's hooks.</summary>
public sealed partial class Window
{
    private TmuxHooks? _hooks;

    /// <summary>Gets the hooks of this window.</summary>
    [UnsupportedOSPlatform("windows")]
    public TmuxHooks Hooks => _hooks ??= new TmuxHooks(
        _commandDispatcher,
        OptionScope.Window,
        _id.ToString());
}
