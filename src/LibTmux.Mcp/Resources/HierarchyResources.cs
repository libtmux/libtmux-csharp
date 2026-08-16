using System.ComponentModel;
using System.Runtime.Versioning;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <summary>The tmux hierarchy, addressable by URI.</summary>
/// <remarks>
/// <para>
/// The same facts the listing tools answer, reachable without a tool call. A
/// resource is something a client can attach on its own initiative — pinned in
/// a sidebar, refreshed on a timer, offered to the user to include — where a
/// tool only ever runs because the model decided to run it.
/// </para>
/// <para>
/// Each answers JSON text rather than a record: a resource result is content,
/// not a typed value, and returning an object the protocol has no shape for
/// fails at read time rather than at build time.
/// </para>
/// </remarks>
[McpServerResourceType]
[UnsupportedOSPlatform("windows")]
public sealed class HierarchyResources
{
    private const string JsonMime = "application/json";
    private const string TextMime = "text/plain";

    private static readonly JsonSerializerOptions Shape = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    private readonly ReadTools _read;

    /// <summary>Initializes the resources over the reading tools.</summary>
    /// <param name="read">Answers the same questions the resources ask.</param>
    /// <remarks>
    /// Built on the tools rather than beside them so a resource and its
    /// equivalent tool cannot drift into two different answers.
    /// </remarks>
    public HierarchyResources(ReadTools read)
    {
        ArgumentNullException.ThrowIfNull(read);
        _read = read;
    }

    /// <summary>Answers every session, window and pane.</summary>
    /// <param name="cancellationToken">Cancels the tmux queries.</param>
    /// <returns>The hierarchy, as JSON.</returns>
    [McpServerResource(
        UriTemplate = "tmux://hierarchy",
        Name = "tmux_hierarchy",
        Title = "tmux hierarchy",
        MimeType = JsonMime)]
    [Description("Every tmux session, window and pane on the default server.")]
    public async Task<string> HierarchyAsync(CancellationToken cancellationToken = default) =>
        Json(await _read.HierarchyAsync(cancellationToken: cancellationToken).ConfigureAwait(false));

    /// <summary>Answers the sessions.</summary>
    /// <param name="cancellationToken">Cancels the tmux query.</param>
    /// <returns>The sessions, as JSON.</returns>
    [McpServerResource(
        UriTemplate = "tmux://sessions",
        Name = "tmux_sessions",
        Title = "tmux sessions",
        MimeType = JsonMime)]
    [Description("The tmux sessions on the default server.")]
    public async Task<string> SessionsAsync(CancellationToken cancellationToken = default) =>
        Json(await _read.ListSessionsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false));

    /// <summary>Answers the panes of one session.</summary>
    /// <param name="sessionId">The session, by id or name.</param>
    /// <param name="cancellationToken">Cancels the tmux query.</param>
    /// <returns>The panes, as JSON.</returns>
    [McpServerResource(
        UriTemplate = "tmux://sessions/{sessionId}/panes",
        Name = "tmux_session_panes",
        Title = "tmux session panes",
        MimeType = JsonMime)]
    [Description("The panes belonging to one tmux session.")]
    public async Task<string> SessionPanesAsync(
        string sessionId,
        CancellationToken cancellationToken = default) =>
        Json(await _read.ListPanesAsync(session: sessionId, cancellationToken: cancellationToken)
            .ConfigureAwait(false));

    /// <summary>Answers what one pane is showing.</summary>
    /// <param name="paneId">The pane, such as <c>%1</c>.</param>
    /// <param name="cancellationToken">Cancels the tmux query.</param>
    /// <returns>The pane's visible text.</returns>
    [McpServerResource(
        UriTemplate = "tmux://panes/{paneId}/content",
        Name = "tmux_pane_content",
        Title = "tmux pane content",
        MimeType = TextMime)]
    [Description("The text one tmux pane is currently showing.")]
    public async Task<string> PaneContentAsync(
        string paneId,
        CancellationToken cancellationToken = default)
    {
        CaptureResult captured = await _read
            .CapturePaneAsync(paneId: paneId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return captured.Content.ToDisplayString();
    }

    /// <summary>Answers which pane this server runs in.</summary>
    /// <param name="cancellationToken">Cancels the tmux query.</param>
    /// <returns>The pane as JSON, or JSON null when this server runs outside tmux.</returns>
    /// <remarks>
    /// A resource rather than a tool description, so a client that wants to
    /// show the user "the assistant is in this pane" can, and one that does not
    /// pays nothing.
    /// </remarks>
    [McpServerResource(
        UriTemplate = "tmux://self",
        Name = "tmux_self",
        Title = "the pane this server runs in",
        MimeType = JsonMime)]
    [Description("The tmux pane this MCP server is running inside, or null.")]
    public async Task<string> SelfAsync(CancellationToken cancellationToken = default) =>
        Json(await _read.WhoAmIAsync(cancellationToken: cancellationToken).ConfigureAwait(false));

    /// <summary>Answers the tmux servers on this machine.</summary>
    /// <param name="cancellationToken">Cancels the probes.</param>
    /// <returns>The sockets, as JSON.</returns>
    [McpServerResource(
        UriTemplate = "tmux://servers",
        Name = "tmux_servers",
        Title = "tmux servers",
        MimeType = JsonMime)]
    [Description("The tmux servers running for this user, by socket.")]
    public async Task<string> ServersAsync(CancellationToken cancellationToken = default) =>
        Json(await _read.ListServersAsync(cancellationToken).ConfigureAwait(false));

    // A resource that answers nothing still has to answer something: the SDK
    // treats a null result as a fault rather than as an empty reading.
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, Shape);
}
