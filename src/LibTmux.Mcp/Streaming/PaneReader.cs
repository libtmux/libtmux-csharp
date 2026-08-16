using System.Runtime.Versioning;
using ModelContextProtocol;

namespace LibTmux.Mcp;

/// <summary>What a pane has printed, and what is new since last time.</summary>
/// <param name="State">The grid state the read saw.</param>
/// <param name="Lines">The rows the read is reporting.</param>
/// <param name="CursorRows">The rows from the cursor row down, for the next cursor.</param>
/// <param name="LinesMissed">Whether scrollback dropped rows this read never saw.</param>
/// <param name="AnchorLost">Whether the previous position could not be found again.</param>
[UnsupportedOSPlatform("windows")]
internal sealed record PaneRead(
    PaneGridState State,
    IReadOnlyList<string> Lines,
    IReadOnlyList<string> CursorRows,
    bool LinesMissed,
    bool AnchorLost);

/// <summary>Reads a pane so that two reads do not overlap or skip.</summary>
/// <remarks>
/// <para>
/// A pane is a grid, not a log. Rows move up as it scrolls, get rewritten in
/// place by a prompt redraw, and are freed once <c>history-limit</c> is
/// reached. Reading "what is new" therefore has to survive all three, and the
/// only way to know a read was consistent is to check that the grid did not
/// move underneath it.
/// </para>
/// <para>
/// The algorithm mirrors the one proven in the Python server: sample the state
/// before and after each capture and retry when they differ; fall back to a
/// hash search when eviction may have rebased the rows; and say so plainly
/// when the anchor is gone rather than silently reporting the whole screen as
/// new.
/// </para>
/// </remarks>
[UnsupportedOSPlatform("windows")]
internal static class PaneReader
{
    private const int StableReadAttempts = 3;

    /// <summary>Reads what is on screen now, with no previous position.</summary>
    /// <param name="pane">The pane to read.</param>
    /// <param name="baselinePid">The pid the caller last saw, or null on a first read.</param>
    /// <param name="cancellationToken">Cancels the tmux queries.</param>
    /// <returns>The read.</returns>
    internal static async Task<PaneRead> ReadVisibleAsync(
        Pane pane,
        string? baselinePid,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < StableReadAttempts; attempt++)
        {
            PaneGridState before = await RequireStateAsync(pane, cancellationToken)
                .ConfigureAwait(false);
            if (baselinePid is null && before.Dead)
            {
                throw new McpException(
                    $"Pane {pane.Id} is dead: the program in it has exited. "
                    + "Use tmux_respawn_pane to start it again.");
            }

            IReadOnlyList<string> lines = await CaptureAsync(pane, null, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<string> cursorRows = await CaptureCursorRowsAsync(
                    pane,
                    before,
                    cancellationToken)
                .ConfigureAwait(false);
            PaneGridState after = await RequireStateAsync(pane, cancellationToken)
                .ConfigureAwait(false);

            if (before == after)
            {
                return new PaneRead(after, lines, cursorRows, false, baselinePid is not null);
            }
        }

        // A pane printing without pause never gives two matching samples. The
        // last read is still usable text; what it is not is a position anything
        // later can be measured from, so the caller is told the anchor is gone.
        PaneGridState settled = await RequireStateAsync(pane, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<string> busy = await CaptureAsync(pane, null, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<string> busyCursor = await CaptureCursorRowsAsync(
                pane,
                settled,
                cancellationToken)
            .ConfigureAwait(false);
        return new PaneRead(settled, busy, busyCursor, false, true);
    }

    /// <summary>Reads what a pane has printed since a cursor was issued.</summary>
    /// <param name="pane">The pane to read.</param>
    /// <param name="cursor">Where the last read finished.</param>
    /// <param name="cancellationToken">Cancels the tmux queries.</param>
    /// <returns>The read.</returns>
    internal static async Task<PaneRead> ReadSinceAsync(
        Pane pane,
        TailCursor cursor,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < StableReadAttempts; attempt++)
        {
            PaneGridState before = await RequireStateAsync(pane, cancellationToken)
                .ConfigureAwait(false);
            RaiseIfPaneReplaced(pane, before, cursor);

            if (AnchorLost(cursor, before))
            {
                PaneRead missed = await ReadVisibleAsync(pane, cursor.PanePid, cancellationToken)
                    .ConfigureAwait(false);
                return missed with { LinesMissed = true, AnchorLost = true };
            }

            bool trimRisk = TrimRisk(cursor, before);
            int start = cursor.AnchorAbsolute - before.HistorySize;
            IReadOnlyList<string> rows = trimRisk
                ? await CaptureAsync(pane, int.MinValue, cancellationToken).ConfigureAwait(false)
                : start >= before.PaneHeight
                    ? []
                    : await CaptureAsync(pane, start, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<string> cursorRows = await CaptureCursorRowsAsync(
                    pane,
                    before,
                    cancellationToken)
                .ConfigureAwait(false);
            PaneGridState after = await RequireStateAsync(pane, cancellationToken)
                .ConfigureAwait(false);
            RaiseIfPaneReplaced(pane, after, cursor);

            if (before != after)
            {
                continue;
            }

            if (trimRisk)
            {
                int? match = FindUniqueAnchor(rows, cursor);
                if (match is null)
                {
                    PaneRead missed = await ReadVisibleAsync(pane, cursor.PanePid, cancellationToken)
                        .ConfigureAwait(false);
                    return missed with { LinesMissed = true, AnchorLost = true };
                }

                rows = [.. rows.Skip(match.Value)];
            }

            return new PaneRead(after, DropAlreadySeen(rows, cursor), cursorRows, false, false);
        }

        PaneRead busy = await ReadVisibleAsync(pane, cursor.PanePid, cancellationToken)
            .ConfigureAwait(false);
        return busy with { LinesMissed = true, AnchorLost = true };
    }

    /// <summary>Captures rows from a pane.</summary>
    /// <param name="pane">The pane to capture.</param>
    /// <param name="start">
    /// The first row: null for the top of the visible screen, a negative number
    /// for that many rows back into scrollback, or <see cref="int.MinValue" />
    /// for everything tmux still holds.
    /// </param>
    /// <param name="cancellationToken">Cancels the tmux query.</param>
    /// <returns>The rows.</returns>
    internal static async Task<IReadOnlyList<string>> CaptureAsync(
        Pane pane,
        int? start,
        CancellationToken cancellationToken)
    {
        CapturePaneRequest? request = start switch
        {
            null => null,
            int.MinValue => new CapturePaneRequest(startLine: new CapturePanePosition(-32768)),
            int value => new CapturePaneRequest(startLine: new CapturePanePosition(value)),
        };
        return await pane.CaptureAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<string>> CaptureCursorRowsAsync(
        Pane pane,
        PaneGridState state,
        CancellationToken cancellationToken)
    {
        if (state.CursorY >= state.PaneHeight)
        {
            return [];
        }

        return await CaptureAsync(pane, state.CursorY, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<PaneGridState> RequireStateAsync(
        Pane pane,
        CancellationToken cancellationToken)
    {
        PaneGridState? state = await PaneGridState.ReadAsync(pane, cancellationToken)
            .ConfigureAwait(false);
        return state ?? throw new McpException(
            $"tmux did not report the state of pane {pane.Id}. It may have just closed.");
    }

    private static void RaiseIfPaneReplaced(Pane pane, PaneGridState state, TailCursor cursor)
    {
        if (!string.Equals(state.PanePid, cursor.PanePid, StringComparison.Ordinal))
        {
            throw new McpException(
                $"Pane {pane.Id} is running a different process than when the cursor was "
                + "issued, so there is nothing to continue from. Call tmux_tail_pane "
                + "again without a cursor.");
        }
    }

    private static bool AnchorLost(TailCursor cursor, PaneGridState state)
    {
        if (cursor.AnchorAbsolute > state.HistorySize + state.PaneHeight - 1)
        {
            return true;
        }

        // clear-history resets the grid to nothing, which destroys the anchor
        // whatever the pane's height is.
        if (state.HistorySize == 0 && cursor.HistorySize > 0)
        {
            return true;
        }

        // Shrinking history means rows were freed; a taller pane means rows were
        // pulled back out of history into view, which frees nothing.
        return state.HistorySize < cursor.HistorySize && state.PaneHeight <= cursor.PaneHeight;
    }

    private static bool TrimRisk(TailCursor cursor, PaneGridState state)
    {
        if (state.HistoryLimit <= 0)
        {
            return true;
        }

        // tmux frees the oldest history in batches rather than a line at a time,
        // so the risk starts before the limit is reached exactly.
        int batch = Math.Max(state.HistoryLimit / 10, 1);
        int floor = state.HistoryLimit - batch;
        return cursor.HistorySize >= floor || state.HistorySize >= floor;
    }

    private static int? FindUniqueAnchor(IReadOnlyList<string> rows, TailCursor cursor)
    {
        if (cursor.AnchorHash is null)
        {
            return null;
        }

        string[] fingerprint = [cursor.AnchorHash, .. cursor.BelowHashes];
        if (rows.Count < fingerprint.Length)
        {
            return null;
        }

        int? match = null;
        for (int index = 0; index + fingerprint.Length <= rows.Count; index++)
        {
            bool same = true;
            for (int offset = 0; offset < fingerprint.Length; offset++)
            {
                if (!string.Equals(
                    TailCursor.HashLine(rows[index + offset]),
                    fingerprint[offset],
                    StringComparison.Ordinal))
                {
                    same = false;
                    break;
                }
            }

            if (!same)
            {
                continue;
            }

            // Two matches mean the fingerprint does not identify a place. A
            // guess would silently report the wrong rows as new.
            if (match is not null)
            {
                return null;
            }

            match = index;
        }

        return match;
    }

    private static List<string> DropAlreadySeen(
        IReadOnlyList<string> rows,
        TailCursor cursor)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        List<string> kept = [];
        int index = 0;
        if (cursor.AnchorHash is null
            || !string.Equals(TailCursor.HashLine(rows[0]), cursor.AnchorHash, StringComparison.Ordinal))
        {
            // The anchor row was rewritten since it was seen, so it is new text.
            kept.Add(rows[0]);
        }

        index = 1;
        int matched = 0;
        while (matched < cursor.BelowHashes.Count
            && index + matched < rows.Count
            && string.Equals(
                TailCursor.HashLine(rows[index + matched]),
                cursor.BelowHashes[matched],
                StringComparison.Ordinal))
        {
            matched++;
        }

        for (int row = index + matched; row < rows.Count; row++)
        {
            kept.Add(rows[row]);
        }

        return kept;
    }
}
