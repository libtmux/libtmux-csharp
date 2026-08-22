using System.Runtime.Versioning;

namespace LibTmux;

// Reaches the server's own environment.
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
