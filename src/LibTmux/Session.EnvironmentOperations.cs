using System.Runtime.Versioning;

namespace LibTmux;

/// <summary>Reaches this session's environment.</summary>
public sealed partial class Session
{
    private TmuxEnvironment? _environment;

    /// <summary>Gets the environment panes created in this session inherit from.</summary>
    [UnsupportedOSPlatform("windows")]
    public TmuxEnvironment Environment => _environment ??= new TmuxEnvironment(
        _commandDispatcher,
        global: false,
        target: _id.ToString());
}
