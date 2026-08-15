using System.ComponentModel;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <summary>The tmux operations an assistant can ask for.</summary>
/// <remarks>
/// Every tool answers text, because that is what an assistant reads. Reading
/// tools are separated from acting ones so that a caller can see what a tool
/// will do before it does it: listing and capturing change nothing, while
/// sending keys and creating windows do.
/// </remarks>
[McpServerToolType]
[UnsupportedOSPlatform("windows")]
public sealed class TmuxTools
{
    private readonly TmuxConnectionAccessor _connection;

    /// <summary>Initializes the tools against one connection.</summary>
    /// <param name="connection">The server the tools talk to.</param>
    public TmuxTools(TmuxConnectionAccessor connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>Lists the sessions, windows, and panes on the server.</summary>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>The hierarchy, one object per line.</returns>
    [McpServerTool]
    [Description("List every tmux session, window, and pane, with their identifiers.")]
    public async Task<string> ListTmuxAsync(CancellationToken cancellationToken = default)
    {
        Server server = await _connection.GetAsync(cancellationToken).ConfigureAwait(false);
        StringBuilder text = new();
        foreach (Session session in await server.GetSessionsAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            text.Append(CultureInfo.InvariantCulture, $"session {session.Id} {session.Name}");
            text.AppendLine();
            foreach (Window window in await session.GetWindowsAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                text.Append(
                    CultureInfo.InvariantCulture,
                    $"  window {window.Id} {window.Index} {window.Name}");
                text.AppendLine();
                foreach (Pane pane in await window.GetPanesAsync(cancellationToken)
                    .ConfigureAwait(false))
                {
                    text.Append(
                        CultureInfo.InvariantCulture,
                        $"    pane {pane.Id} {pane.Width}x{pane.Height}");
                    text.AppendLine();
                }
            }
        }

        return text.Length == 0 ? "No tmux sessions are running." : text.ToString();
    }

    /// <summary>Reads what is on a pane's screen.</summary>
    /// <param name="paneId">The pane, such as <c>%0</c>.</param>
    /// <param name="includeHistory">Whether scrollback is included.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>The pane's contents.</returns>
    [McpServerTool]
    [Description("Read what a tmux pane is showing, optionally including its scrollback.")]
    public async Task<string> CaptureTmuxPaneAsync(
        [Description("The pane identifier, such as %0.")] string paneId,
        [Description("Include the pane's scrollback as well as the visible screen.")]
        bool includeHistory = false,
        CancellationToken cancellationToken = default)
    {
        Pane pane = await FindPaneAsync(paneId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> lines = await pane.CaptureAsync(
                includeHistory
                    ? new CapturePaneRequest(startLine: new CapturePanePosition(-32768))
                    : null,
                cancellationToken)
            .ConfigureAwait(false);
        return lines.Count == 0 ? "The pane is empty." : string.Join('\n', lines);
    }

    /// <summary>Types text into a pane and presses enter.</summary>
    /// <param name="paneId">The pane, such as <c>%0</c>.</param>
    /// <param name="text">The text to type.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>What the pane shows once the text has been sent.</returns>
    [McpServerTool]
    [Description("Type a command into a tmux pane and run it. This changes the pane.")]
    public async Task<string> RunInTmuxPaneAsync(
        [Description("The pane identifier, such as %0.")] string paneId,
        [Description("The command to type into the pane.")] string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        Pane pane = await FindPaneAsync(paneId, cancellationToken).ConfigureAwait(false);

        // The text is sent literally so that a command containing something
        // tmux would read as a key name still arrives as typed.
        await pane.SendTextAsync(text, cancellationToken: cancellationToken).ConfigureAwait(false);
        await pane.EnterAsync(cancellationToken).ConfigureAwait(false);
        return $"Sent to {paneId}. Read the pane to see what it did.";
    }

    /// <summary>Creates a session.</summary>
    /// <param name="name">What to call it.</param>
    /// <param name="startDirectory">Where its first window starts.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>What was created.</returns>
    [McpServerTool]
    [Description("Create a new tmux session. This changes the server.")]
    public async Task<string> CreateTmuxSessionAsync(
        [Description("The session name. It cannot contain a colon or a full stop.")] string name,
        [Description("The directory the session's first window starts in.")]
        string? startDirectory = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await _connection.GetAsync(cancellationToken).ConfigureAwait(false);
        Session session = await server.CreateSessionAsync(
                new NewSessionRequest(name: name, startDirectory: startDirectory),
                cancellationToken)
            .ConfigureAwait(false);
        return $"Created session {session.Id} named {session.Name}.";
    }

    /// <summary>Creates a window in a session.</summary>
    /// <param name="sessionId">The session, such as <c>$0</c>.</param>
    /// <param name="name">What to call the window.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>What was created.</returns>
    [McpServerTool]
    [Description("Create a new window in a tmux session. This changes the session.")]
    public async Task<string> CreateTmuxWindowAsync(
        [Description("The session identifier, such as $0.")] string sessionId,
        [Description("The window name.")] string? name = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await _connection.GetAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Session> sessions = await server.GetSessionsAsync(cancellationToken)
            .ConfigureAwait(false);
        Session session = sessions.FirstOrDefault(candidate =>
                string.Equals(candidate.Id.ToString(), sessionId, StringComparison.Ordinal))
            ?? throw new McpException($"No tmux session is named {sessionId}.");

        Window window = await session.CreateWindowAsync(
                new NewWindowRequest(name: name),
                cancellationToken)
            .ConfigureAwait(false);
        return $"Created window {window.Id} named {window.Name}.";
    }

    private async Task<Pane> FindPaneAsync(string paneId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paneId);
        Server server = await _connection.GetAsync(cancellationToken).ConfigureAwait(false);
        foreach (Session session in await server.GetSessionsAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            foreach (Window window in await session.GetWindowsAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                foreach (Pane pane in await window.GetPanesAsync(cancellationToken)
                    .ConfigureAwait(false))
                {
                    if (string.Equals(pane.Id.ToString(), paneId, StringComparison.Ordinal))
                    {
                        return pane;
                    }
                }
            }
        }

        // An assistant given a pane that has gone should be told so, rather
        // than have the next tool fail somewhere less obvious.
        throw new McpException($"No tmux pane is named {paneId}.");
    }
}
