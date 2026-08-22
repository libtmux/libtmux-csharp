using System.Runtime.Versioning;

namespace LibTmux;

// Reaches this session's hooks.
public sealed partial class Session
{
    private TmuxHooks? _hooks;

    /// <summary>Gets the hooks of this session.</summary>
    [UnsupportedOSPlatform("windows")]
    public TmuxHooks Hooks => _hooks ??= new TmuxHooks(
        _commandDispatcher,
        OptionScope.Session,
        _id.ToString());
}
