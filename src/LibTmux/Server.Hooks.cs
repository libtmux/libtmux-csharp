using System.Runtime.Versioning;

namespace LibTmux;

// Reaches this server's hooks.
public sealed partial class Server
{
    private TmuxHooks? _hooks;

    /// <summary>Gets the hooks of this server.</summary>
    /// <remarks>
    /// tmux has no server hook table of its own: the global one is it, which is
    /// why these are reached with the global flag rather than a server flag.
    /// </remarks>
    [UnsupportedOSPlatform("windows")]
    public TmuxHooks Hooks => _hooks ??= new TmuxHooks(
        _commandDispatcher,
        OptionScope.Server,
        null);
}
