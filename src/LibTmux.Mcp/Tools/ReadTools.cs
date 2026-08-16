using System.Runtime.Versioning;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <summary>Everything an assistant can ask tmux without changing it.</summary>
/// <remarks>
/// <para>
/// Tier is the class, family is the file. A tool lives in the class matching
/// what it costs to be wrong about — reading, changing, or removing — so the
/// server registers a whole tier or none of it, and a tool above the operator's
/// tier never reaches the model's list to be called by name.
/// </para>
/// <para>
/// Every tool answers a record rather than prose, and each is annotated
/// <c>ReadOnly</c> so a client that gates on the hint does not prompt for a
/// listing.
/// </para>
/// </remarks>
[McpServerToolType]
[UnsupportedOSPlatform("windows")]
public sealed partial class ReadTools
{
    private readonly TmuxConnectionAccessor _connection;
    private readonly ServerPolicy _policy;
    private readonly PaneActivityHub _activity;

    /// <summary>Initializes the reading tools.</summary>
    /// <param name="connection">The servers the tools talk to.</param>
    /// <param name="policy">What the tools are allowed to spend.</param>
    /// <param name="activity">Tells a wait when a pane has printed something.</param>
    public ReadTools(
        TmuxConnectionAccessor connection,
        ServerPolicy policy,
        PaneActivityHub activity)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(activity);
        _connection = connection;
        _policy = policy;
        _activity = activity;
    }

    private Task<Server> ServerAsync(string? socketName, CancellationToken cancellationToken) =>
        _connection.GetAsync(socketName, cancellationToken);
}
