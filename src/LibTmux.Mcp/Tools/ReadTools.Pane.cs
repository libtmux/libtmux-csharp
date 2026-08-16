using System.ComponentModel;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <content>Reading what panes are showing.</content>
[UnsupportedOSPlatform("windows")]
public sealed partial class ReadTools
{
    /// <summary>Reads a pane's content and screen state together.</summary>
    /// <param name="paneId">The pane, or null for the active one.</param>
    /// <param name="maxLines">The most lines to answer, or null for the server default.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux queries.</param>
    /// <returns>The content, the cursor, and what the pane is.</returns>
    [McpServerTool(Name = "tmux_snapshot_pane", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Read a pane's visible content together with its cursor position, size and "
        + "running command, in one call. Prefer this over tmux_capture_pane plus "
        + "tmux_list_panes: it is one round trip and the cursor is guaranteed to "
        + "describe the text returned with it.")]
    public async Task<PaneSnapshot> SnapshotPaneAsync(
        [Description("The pane id, such as %1. Omit for the active pane.")]
        string? paneId = null,
        [Description("The most lines to return, newest kept. Omit for the server default.")]
        int? maxLines = null,
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        Pane pane = await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
            .ConfigureAwait(false);
        PaneRead read = await PaneReader.ReadVisibleAsync(pane, null, cancellationToken)
            .ConfigureAwait(false);

        return new PaneSnapshot(
            Pane: PaneInfo.From(pane, TmuxTargets.CallerPaneId()),
            Content: BoundedText.Fit(
                PaneText.Scrub(read.Lines, pane.Width),
                maxLines ?? _policy.MaxLines,
                _policy.MaxBytes),
            CursorX: await TmuxTargets.DisplayNumberAsync(pane, "#{cursor_x}", cancellationToken)
                .ConfigureAwait(false),
            CursorY: read.State.CursorY,
            AlternateScreen: read.State.AlternateScreen);
    }

    /// <summary>Reads what a pane is showing.</summary>
    /// <param name="paneId">The pane, or null for the active one.</param>
    /// <param name="includeHistory">Whether to read scrollback as well as the screen.</param>
    /// <param name="maxLines">The most lines to answer, or null for the server default.</param>
    /// <param name="joinWrappedLines">Whether a line tmux wrapped is rejoined.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux queries.</param>
    /// <returns>The content.</returns>
    /// <remarks>
    /// <paramref name="joinWrappedLines" /> matters more than it looks. tmux
    /// stores a wrap as a real line break, so text a user typed into a narrow
    /// pane comes back split across rows and a search for it finds nothing.
    /// </remarks>
    [McpServerTool(Name = "tmux_capture_pane", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Read the text a pane is showing, and optionally its scrollback. The newest "
        + "lines are always kept; anything dropped to fit the budget is reported. "
        + "To watch a pane across several turns, use tmux_tail_pane instead — it "
        + "returns only what is new.")]
    public async Task<CaptureResult> CapturePaneAsync(
        [Description("The pane id, such as %1. Omit for the active pane.")]
        string? paneId = null,
        [Description("Read scrollback as well as the visible screen.")]
        bool includeHistory = false,
        [Description("The most lines to return, newest kept. Omit for the server default.")]
        int? maxLines = null,
        [Description(
            "Rejoin a line tmux wrapped. Turn this on when matching text somebody "
            + "typed, which a narrow pane splits across rows.")]
        bool joinWrappedLines = false,
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        Pane pane = await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
            .ConfigureAwait(false);

        CapturePaneRequest request = new(
            startLine: includeHistory ? new CapturePanePosition(-32768) : null,
            joinWrappedLines: joinWrappedLines);
        IReadOnlyList<string> lines = await pane.CaptureAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return new CaptureResult(
            pane.Id.ToString(),
            BoundedText.Fit(
                PaneText.Scrub(lines, pane.Width),
                maxLines ?? _policy.MaxLines,
                _policy.MaxBytes));
    }

    /// <summary>Reads what a pane has printed since the last read.</summary>
    /// <param name="paneId">The pane, or null for the active one.</param>
    /// <param name="cursor">Where the last read finished, or null to start now.</param>
    /// <param name="maxLines">The most lines to answer, or null for the server default.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux queries.</param>
    /// <returns>The new text and a cursor for next time.</returns>
    [McpServerTool(Name = "tmux_tail_pane", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Read only what a pane has printed since the last call. Pass back the cursor "
        + "each time. Use this to watch a long-running process across turns: the "
        + "tenth read costs what the first did, where re-capturing the pane would "
        + "return everything again. Call with no cursor to start watching from now.")]
    public async Task<TailResult> TailPaneAsync(
        [Description("The pane id, such as %1. Omit for the active pane.")]
        string? paneId = null,
        [Description("The cursor from the previous call. Omit to start from what is on screen now.")]
        string? cursor = null,
        [Description("The most lines to return, newest kept. Omit for the server default.")]
        int? maxLines = null,
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        Pane pane = await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
            .ConfigureAwait(false);
        string id = pane.Id.ToString();

        TailCursor? previous = TailCursor.Decode(cursor);
        PaneRead read = previous is null
            ? await PaneReader.ReadVisibleAsync(pane, null, cancellationToken).ConfigureAwait(false)
            : await PaneReader.ReadSinceAsync(pane, previous, cancellationToken).ConfigureAwait(false);

        // A first read establishes a position without spending the caller's
        // budget on a screenful they did not ask for.
        IReadOnlyList<string> lines = previous is null ? [] : read.Lines;

        return new TailResult(
            PaneId: id,
            Content: BoundedText.Fit(
                PaneText.Scrub(lines, pane.Width),
                maxLines ?? _policy.MaxLines,
                _policy.MaxBytes),
            Cursor: TailCursor.Build(id, read.State, read.CursorRows).Encode(),
            LinesMissed: read.LinesMissed,
            AnchorLost: previous is not null && read.AnchorLost);
    }

    /// <summary>Searches what panes are showing.</summary>
    /// <param name="pattern">The regular expression to look for.</param>
    /// <param name="session">A session id or name to narrow to, or null for all of them.</param>
    /// <param name="includeHistory">Whether to search scrollback as well as the screen.</param>
    /// <param name="ignoreCase">Whether case is ignored.</param>
    /// <param name="maxMatchesPerPane">The most matching lines to answer per pane.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux queries.</param>
    /// <returns>The panes that matched.</returns>
    [McpServerTool(Name = "tmux_search_panes", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Find which panes are showing text matching a regular expression. This is the "
        + "tool for 'which pane has the error', 'where is the build running', or any "
        + "question about what a pane CONTAINS — the tmux_list_* tools only see names "
        + "and sizes.")]
    public async Task<SearchResult> SearchPanesAsync(
        [Description("A .NET regular expression to look for.")] string pattern,
        [Description("A session id such as $0, or its name. Omit to search every session.")]
        string? session = null,
        [Description("Search scrollback as well as the visible screen. Slower.")]
        bool includeHistory = false,
        [Description("Ignore case when matching.")] bool ignoreCase = true,
        [Description("The most matching lines to return from any one pane.")]
        int maxMatchesPerPane = 20,
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        Regex regex = CompilePattern(pattern, ignoreCase);

        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Pane> panes = string.IsNullOrWhiteSpace(session)
            ? await server.GetPanesAsync(cancellationToken).ConfigureAwait(false)
            : await (await TmuxTargets.SessionAsync(server, session, cancellationToken)
                    .ConfigureAwait(false))
                .GetPanesAsync(cancellationToken)
                .ConfigureAwait(false);

        CapturePaneRequest request = new(
            startLine: includeHistory ? new CapturePanePosition(-32768) : null,
            joinWrappedLines: true);

        List<PaneMatch> hits = [];
        bool truncated = false;
        foreach (Pane pane in panes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> lines;
            try
            {
                lines = await pane.CaptureAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (LibTmuxException)
            {
                // A pane can close between the listing and the capture. One
                // that has gone cannot match, and cannot fail the search either.
                continue;
            }

            List<MatchedLine> matched = [];
            int visibleTop = lines.Count - pane.Height;
            for (int index = 0; index < lines.Count; index++)
            {
                if (!regex.IsMatch(lines[index]))
                {
                    continue;
                }

                if (matched.Count >= maxMatchesPerPane)
                {
                    truncated = true;
                    break;
                }

                matched.Add(new MatchedLine(index - Math.Max(visibleTop, 0), lines[index]));
            }

            if (matched.Count > 0)
            {
                hits.Add(new PaneMatch(
                    pane.Id.ToString(),
                    pane.Window.Id.ToString(),
                    pane.Session.Id.ToString(),
                    matched));
            }
        }

        return new SearchResult(pattern, panes.Count, hits, truncated);
    }

    /// <summary>Compiles a caller's pattern, refusing one that cannot be run safely.</summary>
    /// <remarks>
    /// The timeout is the point. A pattern a model wrote can backtrack for
    /// minutes on a screenful of text, and there is no way to tell from the
    /// pattern alone which one will.
    /// </remarks>
    internal static Regex CompilePattern(string pattern, bool ignoreCase)
    {
        RegexOptions options = RegexOptions.CultureInvariant
            | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        try
        {
            return new Regex(pattern, options, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException error)
        {
            throw new McpException($"'{pattern}' is not a valid regular expression: {error.Message}");
        }
    }
}
