using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <content>Waiting for a pane to say something, without polling it.</content>
[UnsupportedOSPlatform("windows")]
public sealed partial class ReadTools
{
    /// <summary>Waits until a pane prints text a caller is looking for.</summary>
    /// <param name="paneId">The pane, or null for the active one.</param>
    /// <param name="patterns">What to wait for, or null for any output at all.</param>
    /// <param name="stopPatterns">What means waiting is pointless.</param>
    /// <param name="timeoutSeconds">How long to wait, before the server's ceiling.</param>
    /// <param name="ignoreCase">Whether case is ignored.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>How the wait ended and what the pane showed.</returns>
    /// <remarks>
    /// For a command the caller wrote, <c>tmux_run</c> is better: it knows
    /// exactly when the command finished and what it exited with, where this
    /// can only recognise text. This is for output nobody here authored — a
    /// server starting up, a build another process launched, a person typing.
    /// </remarks>
    [McpServerTool(Name = "tmux_wait_for_text", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Wait until a pane prints something matching one of these patterns, then "
        + "return. Use for output you did NOT start — a server's ready line, another "
        + "process's progress, a person typing. For a command you are running "
        + "yourself, tmux_run is better: it reports the real exit status instead of "
        + "guessing from text. Omit patterns to wait for any new output at all. "
        + "Never poll tmux_capture_pane in a loop; this call does the waiting.")]
    public async Task<WaitResult> WaitForTextAsync(
        [Description("The pane id, such as %1. Omit for the active pane.")]
        string? paneId = null,
        [Description(
            "Regular expressions to wait for. Omit or pass an empty list to return as "
            + "soon as the pane prints anything new.")]
        IReadOnlyList<string>? patterns = null,
        [Description(
            "Regular expressions meaning the thing you are waiting for will never "
            + "come, such as an error line. Matching one ends the wait as 'stopped'.")]
        IReadOnlyList<string>? stopPatterns = null,
        [Description(
            "Seconds to wait. Lowered to the server's ceiling; read "
            + "effective_timeout_seconds for the value actually used.")]
        double? timeoutSeconds = null,
        [Description("Ignore case when matching.")] bool ignoreCase = true,
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        Pane pane = await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
            .ConfigureAwait(false);
        string id = pane.Id.ToString();

        Regex[] wanted = Compile(patterns, ignoreCase);
        Regex[] stops = Compile(stopPatterns, ignoreCase);
        TimeSpan budget = _policy.EffectiveTimeout(
            timeoutSeconds is double seconds ? TimeSpan.FromSeconds(seconds) : null);

        Stopwatch elapsed = Stopwatch.StartNew();

        // The lease turns this from a poll into a sleep: tmux reports the
        // pane's output as it happens, and the loop below wakes on it.
        await using IAsyncDisposable lease = await _activity.WatchAsync(pane, cancellationToken)
            .ConfigureAwait(false);

        PaneRead first = await PaneReader.ReadVisibleAsync(pane, null, cancellationToken)
            .ConfigureAwait(false);
        TailCursor cursor = TailCursor.Build(id, first.State, first.CursorRows);
        bool alternate = first.State.AlternateScreen;

        while (true)
        {
            TimeSpan remaining = budget - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return await FinishAsync(
                        pane,
                        id,
                        WaitOutcome.Timeout,
                        null,
                        elapsed,
                        budget,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            // Taken before the read, so output arriving during the read wakes
            // the next wait instead of being slept through.
            object? signal = _activity.CaptureSignal(id);

            PaneRead read = await PaneReader.ReadSinceAsync(pane, cursor, cancellationToken)
                .ConfigureAwait(false);
            cursor = TailCursor.Build(id, read.State, read.CursorRows);

            if (read.Lines.Count > 0)
            {
                if (Match(stops, read.Lines) is string stopped)
                {
                    return await FinishAsync(
                            pane,
                            id,
                            WaitOutcome.Stopped,
                            stopped,
                            elapsed,
                            budget,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                if (wanted.Length == 0)
                {
                    return await FinishAsync(
                            pane,
                            id,
                            WaitOutcome.AnyOutput,
                            null,
                            elapsed,
                            budget,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                if (Match(wanted, read.Lines) is string hit)
                {
                    return await FinishAsync(
                            pane,
                            id,
                            WaitOutcome.Matched,
                            hit,
                            elapsed,
                            budget,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            if (read.State.Dead)
            {
                return await FinishAsync(
                        pane,
                        id,
                        WaitOutcome.PaneDied,
                        null,
                        elapsed,
                        budget,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            // A full-screen program repaints rather than appending, so "what is
            // new" stops meaning anything. Saying so beats waiting out the
            // whole budget for a line that will never arrive as new text.
            if (!alternate && read.State.AlternateScreen)
            {
                return await FinishAsync(
                        pane,
                        id,
                        WaitOutcome.Timeout,
                        null,
                        elapsed,
                        budget,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await _activity.WaitForActivityAsync(
                    id,
                    signal,
                    budget - elapsed.Elapsed,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Waits on a tmux wait-for channel.</summary>
    /// <param name="channel">The channel name.</param>
    /// <param name="timeoutSeconds">How long to wait, before the server's ceiling.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>What happened.</returns>
    /// <remarks>
    /// tmux's own rendezvous, exposed for a shell command a caller composed
    /// themselves. <c>tmux_run</c> uses this internally, so reach for this only
    /// when the command's shape does not fit that tool.
    /// </remarks>
    [McpServerTool(Name = "tmux_wait_for_channel", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Block until something signals a tmux wait-for channel with "
        + "'tmux wait-for -S <channel>'. Use when you composed a shell command that "
        + "signals it. For an ordinary command whose completion you want, tmux_run "
        + "already does this and also reports the exit status.")]
    public async Task<ActionResult> WaitForChannelAsync(
        [Description("The channel name to wait on.")] string channel,
        [Description("Seconds to wait. Lowered to the server's ceiling.")]
        double? timeoutSeconds = null,
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        TimeSpan budget = _policy.EffectiveTimeout(
            timeoutSeconds is double seconds ? TimeSpan.FromSeconds(seconds) : null);

        using CancellationTokenSource expiry = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        expiry.CancelAfter(budget);

        try
        {
            await server.WaitForAsync(
                    new WaitForRequest(channel, TmuxWaitMode.Wait),
                    expiry.Token)
                .ConfigureAwait(false);
            return new ActionResult($"Channel '{channel}' was signalled.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ActionResult(
                $"Channel '{channel}' was not signalled within "
                + $"{budget.TotalSeconds:0.#}s. Nothing was changed; call again to keep waiting.");
        }
    }

    private static Regex[] Compile(IReadOnlyList<string>? patterns, bool ignoreCase) =>
        patterns is null
            ? []
            : [.. patterns
                .Where(each => !string.IsNullOrEmpty(each))
                .Select(each => CompilePattern(each, ignoreCase))];

    private static string? Match(Regex[] patterns, IReadOnlyList<string> lines)
    {
        foreach (Regex pattern in patterns)
        {
            foreach (string line in lines)
            {
                try
                {
                    if (pattern.IsMatch(line))
                    {
                        return pattern.ToString();
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    throw new McpException(
                        $"The pattern '{pattern}' took too long to match. Simplify it — "
                        + "nested quantifiers such as (a+)+ backtrack badly on terminal text.");
                }
            }
        }

        return null;
    }

    private async Task<WaitResult> FinishAsync(
        Pane pane,
        string paneId,
        WaitOutcome outcome,
        string? matched,
        Stopwatch elapsed,
        TimeSpan budget,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> tail = await PaneReader.CaptureAsync(pane, null, cancellationToken)
            .ConfigureAwait(false);
        return new WaitResult(
            PaneId: paneId,
            Outcome: outcome,
            MatchedPattern: matched,
            Tail: BoundedText.Fit(PaneText.Scrub(tail, pane.Width), TailLines, _policy.MaxBytes),
            ElapsedSeconds: Math.Round(elapsed.Elapsed.TotalSeconds, 3),
            EffectiveTimeoutSeconds: budget.TotalSeconds);
    }

    /// <summary>How much of the pane a wait reports back when it ends.</summary>
    /// <remarks>
    /// Enough to see what happened, not enough to be a capture. A caller who
    /// wants the pane can read it; a caller who does not should not pay for it.
    /// </remarks>
    private const int TailLines = 20;
}
