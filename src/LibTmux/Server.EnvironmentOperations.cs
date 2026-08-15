using System.Runtime.Versioning;

namespace LibTmux;

/// <summary>Reaches the server's own environment.</summary>
public sealed partial class Server
{
    private TmuxEnvironment? _environment;

    /// <summary>Gets the environment new sessions inherit from.</summary>
    [UnsupportedOSPlatform("windows")]
    public TmuxEnvironment Environment => _environment ??= new TmuxEnvironment(
        _commandDispatcher,
        global: true,
        target: null);
}
