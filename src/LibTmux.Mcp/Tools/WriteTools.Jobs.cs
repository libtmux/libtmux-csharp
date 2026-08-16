using System.ComponentModel;
using System.Runtime.Versioning;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <content>Commands that outlive the call that started them.</content>
[UnsupportedOSPlatform("windows")]
public sealed partial class WriteTools
{
    /// <summary>Starts a command without waiting for it.</summary>
    /// <param name="command">The shell command.</param>
    /// <param name="paneId">The pane, or null for the active one.</param>
    /// <param name="suppressHistory">Whether to keep the command out of shell history.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels sending the command.</param>
    /// <returns>The handle to collect it with.</returns>
    [McpServerTool(Name = "tmux_start_job", Destructive = false, OpenWorld = true, UseStructuredContent = true)]
    [Description(
        "Start a shell command in a pane and return a job handle IMMEDIATELY, without "
        + "waiting. Use for anything that may run longer than a few seconds — a build, "
        + "a test suite, a deploy — so you can do other work and collect the result "
        + "later with tmux_job. The command keeps running in the pane regardless of "
        + "what you do next.")]
    public async Task<JobInfo> StartJobAsync(
        [Description("The shell command to run.")] string command,
        [Description("The pane id, such as %1. Omit for the active pane.")]
        string? paneId = null,
        [Description("Keep the command out of the shell's history. Best-effort.")]
        bool suppressHistory = true,
        [Description("The tmux socket to use. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        Pane pane = await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
            .ConfigureAwait(false);
        return await _jobs.StartAsync(server, pane, command, suppressHistory, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Reads how a job is doing and what it has printed.</summary>
    /// <param name="jobId">The handle.</param>
    /// <param name="waitSeconds">How long to wait for it to finish, if it has not.</param>
    /// <param name="maxLines">The most output lines to answer.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="progress">Reports that the job is still running.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The job and whatever is new in its pane.</returns>
    /// <remarks>
    /// Waiting here is event-driven rather than a sleep loop, so asking with a
    /// <paramref name="waitSeconds" /> costs no more than asking without one
    /// and returns the instant the command finishes.
    /// </remarks>
    [McpServerTool(Name = "tmux_job", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Read a background job's state, exit status, and whatever its pane has printed "
        + "SINCE THE LAST TIME you asked. Optionally wait a few seconds for it to "
        + "finish first. Call this instead of capturing the pane: it returns only new "
        + "output, so watching a long job stays cheap.")]
    public async Task<JobReport> JobAsync(
        [Description("The job handle from tmux_start_job.")] string jobId,
        [Description(
            "Seconds to wait for the job to finish before answering. Omit to answer "
            + "at once with whatever it is doing now.")]
        double? waitSeconds = null,
        [Description("The most output lines to return, newest kept.")]
        int? maxLines = null,
        [Description("The tmux socket to use. Omit for the default server.")]
        string? socketName = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        JobInfo job = _jobs.Get(jobId);
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        Pane pane = await TmuxTargets.PaneAsync(server, job.PaneId, cancellationToken)
            .ConfigureAwait(false);

        if (waitSeconds is double seconds && job.State == JobState.Running)
        {
            TimeSpan budget = _policy.EffectiveTimeout(TimeSpan.FromSeconds(seconds));
            await using IAsyncDisposable lease = await _activity
                .WatchAsync(pane, cancellationToken)
                .ConfigureAwait(false);
            await WaitForFinishAsync(
                    jobId,
                    pane.Id.ToString(),
                    budget,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
            job = _jobs.Get(jobId);
        }

        TailCursor? cursor = TailCursor.Decode(_jobs.CursorFor(jobId));
        PaneRead read = cursor is null
            ? await PaneReader.ReadVisibleAsync(pane, null, cancellationToken).ConfigureAwait(false)
            : await PaneReader.ReadSinceAsync(pane, cursor, cancellationToken).ConfigureAwait(false);

        _jobs.Advance(jobId, TailCursor.Build(pane.Id.ToString(), read.State, read.CursorRows).Encode());

        return new JobReport(
            job,
            BoundedText.Fit(
                PaneText.Scrub(read.Lines, pane.Width),
                maxLines ?? _policy.MaxLines,
                _policy.MaxBytes),
            read.LinesMissed);
    }

    /// <summary>Lists the jobs this server still remembers.</summary>
    /// <returns>The jobs, most recently started first.</returns>
    [McpServerTool(Name = "tmux_list_jobs", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "List the background jobs this server started and still remembers, newest "
        + "first. A job is forgotten when the server restarts, but its command keeps "
        + "running in its pane.")]
    public IReadOnlyList<JobInfo> ListJobs() => _jobs.List();

    /// <summary>Interrupts a job.</summary>
    /// <param name="jobId">The handle.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels sending the interrupt.</param>
    /// <returns>The job.</returns>
    [McpServerTool(Name = "tmux_cancel_job", Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Interrupt a background job by sending its pane Ctrl-C. This is a request, not "
        + "a guarantee: a program that ignores SIGINT keeps running. Check the pane's "
        + "current_command afterwards to see whether it actually stopped.")]
    public async Task<JobInfo> CancelJobAsync(
        [Description("The job handle from tmux_start_job.")] string jobId,
        [Description("The tmux socket to use. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        JobInfo job = _jobs.Get(jobId);
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        Pane pane = await TmuxTargets.PaneAsync(server, job.PaneId, cancellationToken)
            .ConfigureAwait(false);
        return await _jobs.CancelAsync(pane, jobId, cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForFinishAsync(
        string jobId,
        string paneId,
        TimeSpan budget,
        IProgress<ProgressNotificationValue>? progress,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        DateTimeOffset deadline = started + budget;
        while (DateTimeOffset.UtcNow < deadline)
        {
            ReadTools.Report(
                progress,
                DateTimeOffset.UtcNow - started,
                budget,
                $"job {jobId} still running in {paneId}");
            if (_jobs.Get(jobId).State != JobState.Running)
            {
                return;
            }

            object? signal = _activity.CaptureSignal(paneId);
            if (_jobs.Get(jobId).State != JobState.Running)
            {
                return;
            }

            await _activity.WaitForActivityAsync(
                    paneId,
                    signal,
                    deadline - DateTimeOffset.UtcNow,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
