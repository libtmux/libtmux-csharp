using System.Runtime.Versioning;

namespace LibTmux;

// Reaches this pane's hooks.
public sealed partial class Pane
{
    private TmuxHooks? _hooks;

    /// <summary>Gets the hooks of this pane.</summary>
    [UnsupportedOSPlatform("windows")]
    public TmuxHooks Hooks => _hooks ??= new TmuxHooks(
        _commandDispatcher,
        OptionScope.Pane,
        _id.ToString());
}
