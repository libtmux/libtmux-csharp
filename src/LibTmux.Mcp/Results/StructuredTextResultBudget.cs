using System.Text;
using ModelContextProtocol;

namespace LibTmux.Mcp;

/// <summary>Fits terminal text inside a complete structured MCP result.</summary>
internal static class StructuredTextResultBudget
{
    private const int MaximumCorrectionSteps = 5;
    private const int CorrectionSlackBytes = 64;

    /// <summary>Keeps the newest text whose complete structured result fits.</summary>
    internal static T Fit<T>(
        IReadOnlyList<string> lines,
        int? maxLines,
        int maxBytes,
        Func<BoundedText, T> createResult,
        string resultName)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        ArgumentNullException.ThrowIfNull(createResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultName);

        BoundedText fullText = BoundedText.Fit(lines, maxLines, maxBytes);
        T full = createResult(fullText);
        int fullBytes = Size(fullText, createResult);
        if (fullBytes <= maxBytes)
        {
            return full;
        }

        BoundedText emptyText = DropAll(fullText);
        T minimum = createResult(emptyText);
        int minimumBytes = Size(emptyText, createResult);
        if (minimumBytes > maxBytes)
        {
            throw new McpException(
                $"The {resultName} metadata cannot fit this server's {maxBytes} UTF-8 "
                + $"byte limit. Shorten the request or target metadata, or raise "
                + $"{ServerPolicy.MaxBytesVariable} and restart the MCP server.");
        }

        int retainedBytes = JoinedUtf8ByteCount(fullText.Lines);
        if (retainedBytes == 0)
        {
            return minimum;
        }

        int candidateBytes = EstimateBudget(
            retainedBytes,
            maxBytes - minimumBytes,
            fullBytes - minimumBytes,
            retainedBytes);
        candidateBytes = Math.Min(candidateBytes, retainedBytes - 1);
        T best = minimum;
        int bestBytes = minimumBytes;
        int bestBudget = 0;
        bool foundContent = false;
        for (int step = 0; step < MaximumCorrectionSteps && candidateBytes > 0; step++)
        {
            BoundedText candidateText = Merge(
                fullText,
                BoundedText.Fit(fullText.Lines, null, candidateBytes));
            T candidate = createResult(candidateText);
            int candidateSize = Size(candidateText, createResult);
            if (candidateSize <= maxBytes)
            {
                best = candidate;
                bestBytes = candidateSize;
                bestBudget = candidateBytes;
                foundContent |= candidateText.Lines.Count > 0;
                int expanded = EstimateBudget(
                    candidateBytes,
                    maxBytes - minimumBytes,
                    candidateSize - minimumBytes,
                    retainedBytes - 1);
                expanded = Math.Min(expanded, retainedBytes - 1);
                if (expanded <= candidateBytes)
                {
                    break;
                }

                candidateBytes = expanded;
                continue;
            }

            if (candidateBytes == 1)
            {
                break;
            }

            int corrected = EstimateBudget(
                candidateBytes,
                maxBytes - minimumBytes,
                candidateSize - minimumBytes,
                candidateBytes - 1);
            candidateBytes = Math.Min(
                candidateBytes - 1,
                Math.Max(1, corrected - CorrectionSlackBytes));
        }

        if (!foundContent)
        {
            int high = retainedBytes - 1;
            int lastFit = 0;
            int probe = 1;
            while (probe <= high)
            {
                BoundedText probeText = Merge(
                    fullText,
                    BoundedText.Fit(fullText.Lines, null, probe));
                T probeResult = createResult(probeText);
                int probeSize = Size(probeText, createResult);
                if (probeSize > maxBytes)
                {
                    high = probe - 1;
                    break;
                }

                best = probeResult;
                bestBytes = probeSize;
                bestBudget = probe;
                lastFit = probe;
                if (probe == high)
                {
                    break;
                }

                probe = probe > high / 2 ? high : probe * 2;
            }

            int low = lastFit + 1;
            while (low <= high)
            {
                int midpoint = low + ((high - low) / 2);
                BoundedText midpointText = Merge(
                    fullText,
                    BoundedText.Fit(fullText.Lines, null, midpoint));
                T midpointResult = createResult(midpointText);
                int midpointSize = Size(midpointText, createResult);
                if (midpointSize <= maxBytes)
                {
                    best = midpointResult;
                    bestBytes = midpointSize;
                    bestBudget = midpoint;
                    low = midpoint + 1;
                }
                else
                {
                    high = midpoint - 1;
                }
            }
        }

        int headroomThreshold = Math.Max(256, maxBytes / 100);
        if (maxBytes - bestBytes >= headroomThreshold
            && bestBudget < retainedBytes - 1)
        {
            int low = bestBudget + 1;
            int high = retainedBytes - 1;
            while (low <= high)
            {
                int midpoint = low + ((high - low) / 2);
                BoundedText midpointText = Merge(
                    fullText,
                    BoundedText.Fit(fullText.Lines, null, midpoint));
                T midpointResult = createResult(midpointText);
                int midpointSize = Size(midpointText, createResult);
                if (midpointSize <= maxBytes)
                {
                    best = midpointResult;
                    low = midpoint + 1;
                }
                else
                {
                    high = midpoint - 1;
                }
            }
        }

        return best;
    }

    private static int Size<T>(BoundedText text, Func<BoundedText, T> createResult)
    {
        var skeletonText = new BoundedText(
            [],
            text.Truncated,
            text.DroppedLines,
            text.DroppedBytes);
        T skeleton = createResult(skeletonText);
        int skeletonSize = Utf8JsonBudget.GetStructuredToolResultByteCount(
            skeleton,
            ToolJson.Options);
        int textSize = Utf8JsonBudget.GetStructuredJsonFragmentByteCount(
            text,
            ToolJson.Options);
        int skeletonTextSize = Utf8JsonBudget.GetStructuredJsonFragmentByteCount(
            skeletonText,
            ToolJson.Options);
        return checked(skeletonSize + textSize - skeletonTextSize);
    }

    private static BoundedText DropAll(BoundedText text)
    {
        if (text.Lines.Count == 0)
        {
            return text;
        }

        return Merge(text, BoundedText.Fit(text.Lines, 0, 1));
    }

    private static BoundedText Merge(BoundedText earlier, BoundedText later)
    {
        if (!later.Truncated)
        {
            return earlier;
        }

        return new BoundedText(
            later.Lines,
            Truncated: true,
            DroppedLines: checked(earlier.DroppedLines + later.DroppedLines),
            DroppedBytes: checked(earlier.DroppedBytes + later.DroppedBytes));
    }

    private static int EstimateBudget(
        int referenceBytes,
        int availableBytes,
        int variableBytes,
        int maximumBytes)
    {
        if (maximumBytes <= 0)
        {
            return 0;
        }

        if (availableBytes <= 0 || variableBytes <= 0)
        {
            return 1;
        }

        long estimate = (long)referenceBytes * availableBytes / variableBytes;
        return (int)Math.Clamp(estimate, 1, maximumBytes);
    }

    private static int JoinedUtf8ByteCount(IReadOnlyList<string> lines)
    {
        int bytes = 0;
        for (int index = 0; index < lines.Count; index++)
        {
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(lines[index]));
            if (index > 0)
            {
                bytes = checked(bytes + 1);
            }
        }

        return bytes;
    }
}
