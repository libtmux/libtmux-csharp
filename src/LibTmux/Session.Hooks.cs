using System.Runtime.Versioning;

namespace LibTmux;

/// <summary>Reaches this session's hooks.</summary>
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
