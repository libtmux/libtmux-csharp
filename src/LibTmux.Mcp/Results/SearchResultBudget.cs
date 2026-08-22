using ModelContextProtocol;

namespace LibTmux.Mcp;

/// <summary>Builds one search result within global line and byte ceilings.</summary>
internal sealed class SearchResultBudget
{
    private readonly string _pattern;
    private readonly int _maximumPanes;
    private readonly int _maxMatches;
    private readonly int _maxBytes;
    private readonly List<PaneMatch> _panes = [];
    private int _bytes;
    private int _matches;

    /// <summary>Initializes a result budget, reserving its fixed metadata first.</summary>
    internal SearchResultBudget(
        string pattern,
        int maximumPanes,
        int maxMatches,
        int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumPanes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMatches);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        _pattern = pattern;
        _maximumPanes = maximumPanes;
        _maxMatches = maxMatches;
        _maxBytes = maxBytes;

        _bytes = Utf8JsonBudget.GetStructuredToolResultByteCount(
            new SearchResult(
                _pattern,
                _maximumPanes,
                [],
                Truncated: false),
            ToolJson.Options);
        if (_bytes > _maxBytes)
        {
            throw new McpException(
                $"The search pattern alone exceeds this server's {maxBytes} UTF-8 byte "
                + $"limit. Use a shorter pattern or raise {ServerPolicy.MaxBytesVariable}.");
        }
    }

    /// <summary>Adds a matching line or explains why it cannot be added.</summary>
    internal SearchMatchBudgetOutcome TryAdd(
        string paneId,
        string windowId,
        string sessionId,
        List<MatchedLine> paneMatches,
        MatchedLine match)
    {
        ArgumentNullException.ThrowIfNull(paneId);
        ArgumentNullException.ThrowIfNull(windowId);
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(paneMatches);
        ArgumentNullException.ThrowIfNull(match);
        if (_matches >= _maxMatches)
        {
            return SearchMatchBudgetOutcome.GlobalLimit;
        }

        int remainingBytes = _maxBytes - _bytes;
        int minimumCurrentBytes = AddedBytes(
            paneId,
            windowId,
            sessionId,
            paneMatches,
            new MatchedLine(0, string.Empty));
        if (minimumCurrentBytes > remainingBytes)
        {
            bool anotherPaneNeedsComma = _panes.Count > 0 || paneMatches.Count > 0;
            int minimumFuturePaneBytes = checked(
                Utf8JsonBudget.GetStructuredJsonFragmentByteCount(
                    new PaneMatch("%0", "@0", "$0", []),
                    ToolJson.Options)
                + Utf8JsonBudget.GetStructuredJsonFragmentByteCount(
                    new MatchedLine(0, string.Empty),
                    ToolJson.Options)
                + (anotherPaneNeedsComma ? 2 : 0));
            return minimumFuturePaneBytes > remainingBytes
                ? SearchMatchBudgetOutcome.GlobalLimit
                : SearchMatchBudgetOutcome.PaneCannotFit;
        }

        if (match.Text.Length > remainingBytes / 2
            || System.Text.Encoding.UTF8.GetByteCount(match.Text) > remainingBytes / 2
            || Utf8JsonBudget.GetStructuredJsonStringContentByteCount(
                match.Text,
                ToolJson.Options) > remainingBytes)
        {
            return SearchMatchBudgetOutcome.ItemTooLarge;
        }

        int addedBytes = AddedBytes(paneId, windowId, sessionId, paneMatches, match);
        if (addedBytes > remainingBytes)
        {
            return SearchMatchBudgetOutcome.ItemTooLarge;
        }

        _bytes += addedBytes;
        paneMatches.Add(match);
        _matches++;
        return SearchMatchBudgetOutcome.Added;
    }

    /// <summary>Commits the matches accumulated for one pane.</summary>
    internal void Commit(
        string paneId,
        string windowId,
        string sessionId,
        IReadOnlyList<MatchedLine> paneMatches)
    {
        ArgumentNullException.ThrowIfNull(paneMatches);
        if (paneMatches.Count > 0)
        {
            _panes.Add(new PaneMatch(
                paneId,
                windowId,
                sessionId,
                paneMatches.ToArray()));
        }
    }

    /// <summary>Builds the bounded result.</summary>
    internal SearchResult Build(int panesSearched, bool truncated) =>
        new(_pattern, panesSearched, _panes, truncated);

    private int AddedBytes(
        string paneId,
        string windowId,
        string sessionId,
        List<MatchedLine> paneMatches,
        MatchedLine match) =>
        paneMatches.Count == 0
            ? checked(
                Utf8JsonBudget.GetStructuredJsonFragmentByteCount(
                    new PaneMatch(paneId, windowId, sessionId, []),
                    ToolJson.Options)
                + Utf8JsonBudget.GetStructuredJsonFragmentByteCount(match, ToolJson.Options)
                + (_panes.Count > 0 ? 2 : 0))
            : checked(
                Utf8JsonBudget.GetStructuredJsonFragmentByteCount(match, ToolJson.Options)
                + 2);

}

/// <summary>Why a search match was accepted or refused.</summary>
internal enum SearchMatchBudgetOutcome
{
    /// <summary>The match was added.</summary>
    Added = 0,

    /// <summary>This match is too large, but a smaller one can still fit.</summary>
    ItemTooLarge = 1,

    /// <summary>This pane's metadata cannot fit, but a smaller endpoint may.</summary>
    PaneCannotFit = 2,

    /// <summary>No further match can fit the global line or byte budget.</summary>
    GlobalLimit = 3,
}
