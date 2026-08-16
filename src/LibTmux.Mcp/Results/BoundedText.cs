using System.Text;

namespace LibTmux.Mcp;

/// <summary>Terminal text cut to fit a budget, with what was cut reported.</summary>
/// <param name="Lines">The text that fits, oldest first.</param>
/// <param name="Truncated">Whether anything was dropped to make it fit.</param>
/// <param name="DroppedLines">How many lines were dropped from the start.</param>
/// <param name="DroppedBytes">How many UTF-8 bytes were dropped from the start.</param>
/// <remarks>
/// Dropping is always from the oldest end. A terminal's newest line is the one
/// that says what happened, so a budget that discarded it would answer the
/// wrong question. Reporting the loss is what separates a short answer from a
/// wrong one: a reader who cannot see that lines are missing will conclude the
/// pane never printed them.
/// </remarks>
public sealed record BoundedText(
    IReadOnlyList<string> Lines,
    bool Truncated,
    int DroppedLines,
    int DroppedBytes)
{
    /// <summary>An empty result that dropped nothing.</summary>
    public static BoundedText Empty { get; } = new([], false, 0, 0);

    /// <summary>Cuts lines down to a line and byte budget, keeping the newest.</summary>
    /// <param name="lines">The captured text, oldest first.</param>
    /// <param name="maxLines">The most lines to keep, or null for no line limit.</param>
    /// <param name="maxBytes">The most UTF-8 bytes to keep.</param>
    /// <returns>The text that fits, and what it cost to make it fit.</returns>
    public static BoundedText Fit(IReadOnlyList<string> lines, int? maxLines, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        int firstKept = 0;
        if (maxLines is int lineBudget)
        {
            firstKept = Math.Max(0, lines.Count - Math.Max(lineBudget, 0));
        }

        // A joined capture can hold one logical line far wider than the pane,
        // so the byte budget has to be applied after the line budget rather
        // than assumed to follow from it. The newest line is kept even when it
        // alone overruns: answering with nothing is never the better answer.
        int bytes = 0;
        int start = lines.Count;
        for (int index = lines.Count - 1; index >= firstKept; index--)
        {
            int lineBytes = Encoding.UTF8.GetByteCount(lines[index]) + 1;
            if (start < lines.Count && bytes + lineBytes > maxBytes)
            {
                break;
            }

            bytes += lineBytes;
            start = index;
        }

        start = Math.Max(firstKept, start);
        if (start <= 0)
        {
            return new BoundedText(lines, false, 0, 0);
        }

        int droppedBytes = 0;
        for (int index = 0; index < start; index++)
        {
            droppedBytes += Encoding.UTF8.GetByteCount(lines[index]) + 1;
        }

        string[] kept = new string[lines.Count - start];
        for (int index = 0; index < kept.Length; index++)
        {
            kept[index] = lines[start + index];
        }

        return new BoundedText(kept, true, start, droppedBytes);
    }

    /// <summary>Renders the text as one block, noting any loss at the top.</summary>
    /// <returns>The lines joined by newlines, after any truncation notice.</returns>
    /// <remarks>
    /// The notice leads because a reader who sees it will read what follows as
    /// a tail rather than as the whole pane.
    /// </remarks>
    public string ToDisplayString()
    {
        string body = string.Join('\n', Lines);
        return Truncated
            ? $"[{DroppedLines} earlier lines ({DroppedBytes} bytes) omitted to fit the budget]\n{body}"
            : body;
    }
}
