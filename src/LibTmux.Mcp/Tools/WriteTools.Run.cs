using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <content>Running a command in a pane and knowing when it finished.</content>
[UnsupportedOSPlatform("windows")]
public sealed partial class WriteTools
{
    /// <summary>Runs a command in a pane and waits for it to finish.</summary>
    /// <param name="command">The shell command.</param>
    /// <param name="paneId">The pane, or null for the active one.</param>
    /// <param name="timeoutSeconds">How long to wait, before the server's ceiling.</param>
    /// <param name="maxLines">The most output lines to answer.</param>
    /// <param name="suppressHistory">Whether to keep the command out of shell history.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="progress">Reports that the command is still running.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The exit status and what the command printed.</returns>
    /// <remarks>
    /// Completion is not guessed from the text on screen. The command is
    /// followed by a private tmux rendezvous and a private option carrying
    /// <c>$?</c>, so "it finished" and "it exited 1" are facts rather than
    /// readings of a prompt this tool would have to recognise.
    /// </remarks>
    [McpServerTool(Name = "tmux_run", Destructive = false, OpenWorld = true, UseStructuredContent = true)]
    [Description(
        "Run a shell command in a pane, wait for it to finish, and report its real "
        + "exit status and output. This is the tool for 'run X and tell me if it "
        + "worked'. Do NOT send keys and then poll a capture in a loop — this waits "
        + "deterministically and costs one call. The command runs in a subshell, so "
        + "cd and export do not persist. If it may outlast the timeout, use "
        + "tmux_start_job instead and collect it later.")]
    public async Task<RunResult> RunAsync(
        [Description("The shell command to run.")] string command,
        [Description("The pane id, such as %1. Omit for the active pane.")]
        string? paneId = null,
        [Description(
            "Seconds to wait. Lowered to the server's ceiling; read "
            + "effective_timeout_seconds for the value actually used.")]
        double? timeoutSeconds = null,
        [Description("The most output lines to return, newest kept.")]
        int? maxLines = null,
        [Description(
            "Keep the command out of the shell's history by prefixing a space. "
            + "Works on shells set to ignore space-prefixed commands; it is "
            + "best-effort, not a guarantee.")]
        bool suppressHistory = true,
        [Description("The tmux socket to use. Omit for the default server.")]
        string? socketName = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        Pane pane = await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
            .ConfigureAwait(false);
        TimeSpan budget = _policy.EffectiveTimeout(
            timeoutSeconds is double seconds ? TimeSpan.FromSeconds(seconds) : null);

        RunToken token = RunToken.Create();
        Stopwatch elapsed = Stopwatch.StartNew();
        await SendRunPayloadAsync(server, pane, command, token, suppressHistory, cancellationToken)
            .ConfigureAwait(false);

        bool timedOut = !await TickWhileAsync(
                AwaitChannelAsync(server, token.Channel, budget, cancellationToken),
                progress,
                elapsed,
                budget,
                $"running in {pane.Id}",
                cancellationToken)
            .ConfigureAwait(false);
        elapsed.Stop();

        int? status = timedOut
            ? null
            : await ReadStatusAsync(pane, token, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> lines = await pane.CaptureAsync(
                new CapturePaneRequest(joinWrappedLines: true),
                cancellationToken)
            .ConfigureAwait(false);

        return new RunResult(
            PaneId: pane.Id.ToString(),
            ExitStatus: status,
            TimedOut: timedOut,
            Output: BoundedText.Fit(
                PaneText.Scrub(lines, pane.Width),
                maxLines ?? _policy.MaxLines,
                _policy.MaxBytes),
            ElapsedSeconds: Math.Round(elapsed.Elapsed.TotalSeconds, 3),
            EffectiveTimeoutSeconds: budget.TotalSeconds);
    }

    /// <summary>Names the private channel and option one run uses.</summary>
    /// <param name="Id">What makes this run's names unique.</param>
    /// <remarks>
    /// Unique per run so two commands in the same pane cannot answer each
    /// other's rendezvous, which would report one command's exit status for
    /// the other's work.
    /// </remarks>
    internal readonly record struct RunToken(string Id)
    {
        /// <summary>Gets the wait-for channel this run signals.</summary>
        internal string Channel => $"lt_r_{Id}";

        /// <summary>Gets the pane option this run leaves its exit status in.</summary>
        internal string StatusOption => $"@lt_s_{Id}";

        /// <summary>Mints a token nothing else is using.</summary>
        /// <returns>The token.</returns>
        internal static RunToken Create() => new(Guid.NewGuid().ToString("N")[..10]);
    }

    internal static async Task SendRunPayloadAsync(
        Server server,
        Pane pane,
        string command,
        RunToken token,
        bool suppressHistory,
        CancellationToken cancellationToken)
    {
        string statusCommand = TmuxCommandLine(
            server,
            "set-option",
            "-p",
            "-t",
            pane.Id.ToString(),
            token.StatusOption);
        string signalCommand = TmuxCommandLine(server, "wait-for", "-S", token.Channel);

        // The command goes in a subshell so that its own syntax cannot run into
        // the bookkeeping after it: an unbalanced quote or a trailing operator
        // would otherwise swallow the status capture and the rendezvous, and the
        // wait would hang for the whole budget with nothing to show.
        string payload = string.Concat(
            suppressHistory ? " " : string.Empty,
            "(\n",
            command.TrimEnd(),
            "\n); __lt=$?; ",
            statusCommand,
            " \"$__lt\"; ",
            signalCommand);

        await pane.SendKeysAsync(
                new SendKeysRequest(text: payload, enter: true, literal: true),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Reports progress on a beat while one wait runs.</summary>
    /// <param name="waiting">The wait to watch.</param>
    /// <param name="progress">Where to report, or null when the client asked for none.</param>
    /// <param name="elapsed">How long the wait has run.</param>
    /// <param name="budget">How long it may run.</param>
    /// <param name="message">What to say it is doing.</param>
    /// <param name="cancellationToken">Stops the beat.</param>
    /// <returns>Whatever the wait answered.</returns>
    /// <remarks>
    /// The wait itself is one call with nothing to iterate, so the beat comes
    /// from a timer rather than from the work. It costs nothing when the
    /// client asked for no progress, which is the common case.
    /// </remarks>
    internal static async Task<bool> TickWhileAsync(
        Task<bool> waiting,
        IProgress<ProgressNotificationValue>? progress,
        Stopwatch elapsed,
        TimeSpan budget,
        string message,
        CancellationToken cancellationToken)
    {
        if (progress is null)
        {
            return await waiting.ConfigureAwait(false);
        }

        while (true)
        {
            Task beat = Task.Delay(ProgressInterval, cancellationToken);
            if (await Task.WhenAny(waiting, beat).ConfigureAwait(false) == waiting)
            {
                return await waiting.ConfigureAwait(false);
            }

            ReadTools.Report(progress, elapsed.Elapsed, budget, message);
        }
    }

    /// <summary>How often a caller is told a wait is still running.</summary>
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(1);

    internal static async Task<bool> AwaitChannelAsync(
        Server server,
        string channel,
        TimeSpan budget,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource expiry = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        expiry.CancelAfter(budget);
        try
        {
            await server.WaitForAsync(new WaitForRequest(channel, TmuxWaitMode.Wait), expiry.Token)
                .ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    internal static async Task<int?> ReadStatusAsync(
        Pane pane,
        RunToken token,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TmuxOption> options = await pane.Options
            .GetAsync(new GetOptionRequest(token.StatusOption, quiet: true), cancellationToken)
            .ConfigureAwait(false);

        int? status = options.Count > 0
            && int.TryParse(
                options[0].Value.Raw,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsed)
                ? parsed
                : null;

        try
        {
            await pane.Options
                .UnsetAsync(new UnsetOptionRequest(token.StatusOption, quiet: true), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (LibTmuxException)
        {
            // The option is scoped to a pane that may already be gone. Leaving
            // one behind is untidy; failing the call over it would be worse.
        }

        return status;
    }
}
