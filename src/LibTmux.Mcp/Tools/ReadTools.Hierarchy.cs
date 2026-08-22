using System.ComponentModel;
using System.Runtime.Versioning;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <content>Reading what the server, its sessions, windows and panes are.</content>
[UnsupportedOSPlatform("windows")]
public sealed partial class ReadTools
{
    /// <summary>Reads the whole hierarchy in one call.</summary>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux queries.</param>
    /// <returns>Every session, window and pane, as flat lists.</returns>
    [McpServerTool(Name = "tmux_hierarchy", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Read every tmux session, window and pane at once. Start here when you do not "
        + "know what exists. Each entity names its parent, and the pane marked "
        + "isCaller is the one this server runs in. For one level only, the "
        + "tmux_list_* tools are cheaper.")]
    public async Task<HierarchyView> HierarchyAsync(
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        string? caller = TmuxTargets.CallerPaneId();

        IReadOnlyList<Session> sessions = await TmuxAvailability
            .OrEmptyAsync(server, () => server.GetSessionsAsync(cancellationToken))
            .ConfigureAwait(false);
        IReadOnlyList<Window> windows = await TmuxAvailability
            .OrEmptyAsync(server, () => server.GetWindowsAsync(cancellationToken))
            .ConfigureAwait(false);
        IReadOnlyList<Pane> panes = await TmuxAvailability
            .OrEmptyAsync(server, () => server.GetPanesAsync(cancellationToken))
            .ConfigureAwait(false);

        return new HierarchyView(
            [.. sessions.Select(SessionInfo.From)],
            [.. windows.Select(WindowInfo.From)],
            [.. panes.Select(pane => PaneInfo.From(pane, caller))]);
    }

    /// <summary>Reads what the server is.</summary>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux queries.</param>
    /// <returns>The server's version and how much it holds.</returns>
    [McpServerTool(Name = "tmux_server_info", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Read the tmux server's version and how many sessions, windows and panes it "
        + "holds. Use to confirm a socket is alive and which tmux is running it.")]
    public async Task<TmuxServerInfo> ServerInfoAsync(
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Session> sessions = await TmuxAvailability
            .OrEmptyAsync(server, () => server.GetSessionsAsync(cancellationToken))
            .ConfigureAwait(false);
        IReadOnlyList<Window> windows = await TmuxAvailability
            .OrEmptyAsync(server, () => server.GetWindowsAsync(cancellationToken))
            .ConfigureAwait(false);
        IReadOnlyList<Pane> panes = await TmuxAvailability
            .OrEmptyAsync(server, () => server.GetPanesAsync(cancellationToken))
            .ConfigureAwait(false);

        return new TmuxServerInfo(
            SocketName: server.ConnectionOptions.SocketName ?? _connection.DefaultSocketName,
            Version: sessions.Count == 0 ? null : server.Version?.ToString(),
            SessionCount: sessions.Count,
            WindowCount: windows.Count,
            PaneCount: panes.Count,
            CallerPaneId: TmuxTargets.CallerPaneId());
    }

    /// <summary>Lists the sessions.</summary>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux query.</param>
    /// <returns>Every session on the server.</returns>
    [McpServerTool(Name = "tmux_list_sessions", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "List the tmux sessions. This reads names and sizes, not terminal text — to "
        + "find what a pane is showing, use tmux_search_panes.")]
    public async Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Session> sessions = await TmuxAvailability
            .OrEmptyAsync(server, () => server.GetSessionsAsync(cancellationToken))
            .ConfigureAwait(false);
        return [.. sessions.Select(SessionInfo.From)];
    }

    /// <summary>Lists the windows.</summary>
    /// <param name="session">A session id or name to narrow to, or null for all of them.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux query.</param>
    /// <returns>The windows.</returns>
    [McpServerTool(Name = "tmux_list_windows", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "List tmux windows, optionally within one session. This reads names and "
        + "layouts, not terminal text — to find what a pane is showing, use "
        + "tmux_search_panes.")]
    public async Task<IReadOnlyList<WindowInfo>> ListWindowsAsync(
        [Description("A session id such as $0, or its name. Omit for every session.")]
        string? session = null,
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(session))
        {
            IReadOnlyList<Window> all = await TmuxAvailability
                .OrEmptyAsync(server, () => server.GetWindowsAsync(cancellationToken))
                .ConfigureAwait(false);
            return [.. all.Select(WindowInfo.From)];
        }

        Session scoped = await TmuxTargets.SessionAsync(server, session, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<Window> windows = await scoped.GetWindowsAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. windows.Select(WindowInfo.From)];
    }

    /// <summary>Lists the panes.</summary>
    /// <param name="session">A session id or name to narrow to, or null for all of them.</param>
    /// <param name="windowId">A window id to narrow to, or null for all of them.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux query.</param>
    /// <returns>The panes.</returns>
    [McpServerTool(Name = "tmux_list_panes", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "List tmux panes, optionally within one session or window. Filter for "
        + "isCaller=true to answer 'which pane am I in?'. This reads sizes and "
        + "running commands, not terminal text — for that use tmux_search_panes.")]
    public async Task<IReadOnlyList<PaneInfo>> ListPanesAsync(
        [Description("A session id such as $0, or its name. Omit for every session.")]
        string? session = null,
        [Description("A window id such as @0. Omit for every window.")]
        string? windowId = null,
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        string? caller = TmuxTargets.CallerPaneId();

        if (!string.IsNullOrWhiteSpace(windowId))
        {
            Window window = await TmuxTargets.WindowAsync(server, windowId, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<Pane> scoped = await window.GetPanesAsync(cancellationToken)
                .ConfigureAwait(false);
            return [.. scoped.Select(pane => PaneInfo.From(pane, caller))];
        }

        if (!string.IsNullOrWhiteSpace(session))
        {
            Session owner = await TmuxTargets.SessionAsync(server, session, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<Pane> scoped = await owner.GetPanesAsync(cancellationToken)
                .ConfigureAwait(false);
            return [.. scoped.Select(pane => PaneInfo.From(pane, caller))];
        }

        IReadOnlyList<Pane> panes = await TmuxAvailability
            .OrEmptyAsync(server, () => server.GetPanesAsync(cancellationToken))
            .ConfigureAwait(false);
        return [.. panes.Select(pane => PaneInfo.From(pane, caller))];
    }

    /// <summary>Answers which pane this server is running inside.</summary>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux query.</param>
    /// <returns>The pane, or null when this server does not run inside one.</returns>
    /// <remarks>
    /// tmux sets <c>TMUX_PANE</c> in every pane it starts, so this needs no
    /// guessing when the client that launched this server was itself in tmux.
    /// </remarks>
    [McpServerTool(Name = "tmux_whoami", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Answer which pane this MCP server is running inside, or null when it is not "
        + "running in tmux. Use before sending keys anywhere, so you do not type into "
        + "your own terminal by mistake.")]
    public async Task<PaneInfo?> WhoAmIAsync(
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        Pane? caller = await TmuxTargets.CallerPaneAsync(server, cancellationToken)
            .ConfigureAwait(false);
        return caller is null ? null : PaneInfo.From(caller, caller.Id.ToString());
    }
}
