using System.ComponentModel;
using System.Runtime.Versioning;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <summary>The tools that remove what they act on.</summary>
/// <remarks>
/// <para>
/// Registered only when the operator sets the tier to <c>destructive</c>.
/// They are separate from <see cref="WriteTools" /> because the two are not
/// the same risk: a split that lands in the wrong window is a nuisance, and a
/// kill that lands in the wrong session is somebody's work.
/// </para>
/// <para>
/// Nothing here is recoverable. tmux keeps no undo, and a killed pane's
/// scrollback goes with it.
/// </para>
/// </remarks>
[McpServerToolType]
[UnsupportedOSPlatform("windows")]
public sealed class DestructiveTools
{
    private readonly TmuxConnectionAccessor _connection;

    /// <summary>Initializes the removing tools.</summary>
    /// <param name="connection">The servers the tools talk to.</param>
    public DestructiveTools(TmuxConnectionAccessor connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>Kills a pane.</summary>
    /// <param name="paneId">The pane.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What was removed.</returns>
    [McpServerTool(Name = "tmux_kill_pane", Destructive = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Kill a pane and everything running in it. Its scrollback goes with it and "
        + "cannot be recovered. Killing a window's last pane closes the window.")]
    public async Task<ActionResult> KillPaneAsync(
        [Description("The pane id to kill, such as %1.")] string paneId,
        [Description("The tmux socket to use. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await _connection.GetAsync(socketName, cancellationToken)
            .ConfigureAwait(false);
        Pane pane = await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
            .ConfigureAwait(false);
        string id = pane.Id.ToString();
        await pane.KillAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return new ActionResult($"Killed pane {id}.");
    }

    /// <summary>Kills a window.</summary>
    /// <param name="windowId">The window.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What was removed.</returns>
    [McpServerTool(Name = "tmux_kill_window", Destructive = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Kill a window and every pane in it. Killing a session's last window ends the "
        + "session. None of it can be recovered.")]
    public async Task<ActionResult> KillWindowAsync(
        [Description("The window id to kill, such as @1.")] string windowId,
        [Description("The tmux socket to use. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await _connection.GetAsync(socketName, cancellationToken)
            .ConfigureAwait(false);
        Window window = await TmuxTargets.WindowAsync(server, windowId, cancellationToken)
            .ConfigureAwait(false);
        string id = window.Id.ToString();
        await window.KillAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return new ActionResult($"Killed window {id}.");
    }

    /// <summary>Kills a session.</summary>
    /// <param name="session">The session, by id or name.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What was removed.</returns>
    [McpServerTool(Name = "tmux_kill_session", Destructive = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Kill a session, every window in it, and everything running in those windows. "
        + "This is somebody's work if you did not create it — check tmux_list_panes "
        + "for what is running first. It cannot be recovered.")]
    public async Task<ActionResult> KillSessionAsync(
        [Description("A session id such as $0, or its name.")] string session,
        [Description("The tmux socket to use. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(session);
        Server server = await _connection.GetAsync(socketName, cancellationToken)
            .ConfigureAwait(false);
        Session target = await TmuxTargets.SessionAsync(server, session, cancellationToken)
            .ConfigureAwait(false);
        string id = target.Id.ToString();
        string name = target.Name;
        await target.KillAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return new ActionResult($"Killed session {id} ({name}).");
    }

    /// <summary>Kills the whole server.</summary>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What was removed.</returns>
    /// <remarks>
    /// This ends every session on the socket, including ones nobody here
    /// created. There is no narrower way to say it, which is why the tool
    /// exists at the top tier and nowhere else.
    /// </remarks>
    [McpServerTool(Name = "tmux_kill_server", Destructive = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Kill the entire tmux server: every session, window and pane on that socket, "
        + "including work nobody here started. Almost always the wrong tool — kill "
        + "the one session you mean instead.")]
    public async Task<ActionResult> KillServerAsync(
        [Description("The tmux socket to kill. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await _connection.GetAsync(socketName, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<Session> sessions = await server.GetSessionsAsync(cancellationToken)
            .ConfigureAwait(false);
        await server.KillAsync(cancellationToken).ConfigureAwait(false);
        return new ActionResult(
            $"Killed the tmux server on socket '{socketName ?? _connection.DefaultSocketName ?? "default"}' "
            + $"and the {sessions.Count} session(s) it held.");
    }
}
