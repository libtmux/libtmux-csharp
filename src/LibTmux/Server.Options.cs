using System.Runtime.Versioning;
using LibTmux.Internal;

namespace LibTmux;

/// <summary>Reaches the server's own option table.</summary>
public sealed partial class Server
{
    private TmuxOptions? _options;

    /// <summary>Gets the options of this server.</summary>
    /// <remarks>
    /// Server options belong to the daemon rather than to anything inside it,
    /// so nothing is targeted and the global table is the only one there is.
    /// </remarks>
    [UnsupportedOSPlatform("windows")]
    public TmuxOptions Options => _options ??= new TmuxOptions(
        _commandDispatcher,
        OptionScope.Server,
        null,
        DoubleEscapesDollar(this));

    private static bool DoubleEscapesDollar(Server? owner) =>
        owner?.Version is TmuxVersion version
        && TmuxCapabilities.TryGetExact(version, out TmuxCapabilityProfile? profile)
        && profile.Capabilities.Contains("option_dollar_double_escape");
}
