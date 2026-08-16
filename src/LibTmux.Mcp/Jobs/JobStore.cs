using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace LibTmux.Mcp;

/// <summary>Commands that keep running after the call that started them returned.</summary>
/// <remarks>
/// <para>
/// A wait bounded by the server's ceiling is the right shape for a command
/// that takes seconds. It is the wrong shape for a build: the model spends its
/// whole turn asleep, and the protocol gives it no way to change its mind
/// halfway. A job inverts that — starting one returns immediately with a
/// handle, the command runs on in the pane, and the model does something else
/// and collects the result when it is ready.
/// </para>
/// <para>
/// tmux is what keeps the command alive, not this process. What is held here
/// is only the bookkeeping needed to recognise the finish and read the output:
/// if this server is restarted the command carries on, and only the handle is
/// lost.
/// </para>
/// </remarks>
[UnsupportedOSPlatform("windows")]
public sealed class JobStore : IDisposable
{
    private const int MaxRetained = 100;

    private readonly ConcurrentDictionary<string, Job> _jobs = new(StringComparer.Ordinal);
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _shutdown = new();

    /// <summary>Initializes the store.</summary>
    /// <param name="logger">Records how a job ended.</param>
    public JobStore(ILogger? logger = null) => _logger = logger;

    /// <inheritdoc />
    public void Dispose()
    {
        // The commands themselves are tmux's; only the watchers stop.
        _shutdown.Cancel();
        _shutdown.Dispose();
    }

    /// <summary>Starts a command and answers a handle for it immediately.</summary>
    /// <param name="server">The server the pane belongs to.</param>
    /// <param name="pane">The pane to run in.</param>
    /// <param name="command">The shell command.</param>
    /// <param name="suppressHistory">Whether to keep the command out of shell history.</param>
    /// <param name="cancellationToken">Cancels sending the command.</param>
    /// <returns>The job.</returns>
    public async Task<JobInfo> StartAsync(
        Server server,
        Pane pane,
        string command,
        bool suppressHistory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(pane);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        // The handle IS the run token's id, so a caller holding a job id can be
        // matched against the bookkeeping lines that job left in its pane.
        WriteTools.RunToken token = WriteTools.RunToken.Create();
        Job job = new(token.Id, pane.Id.ToString(), command, token);
        Forget();
        _jobs[job.JobId] = job;

        await WriteTools
            .SendRunPayloadAsync(server, pane, command, token, suppressHistory, cancellationToken)
            .ConfigureAwait(false);

        // Watching is deliberately detached: the point of a job is that the
        // caller does not wait, so nothing here may be awaited by the tool call.
        job.Watcher = Task.Run(() => WatchAsync(server, pane, job), CancellationToken.None);
        return job.Describe();
    }

    /// <summary>Answers what a job is doing.</summary>
    /// <param name="jobId">The handle.</param>
    /// <returns>The job.</returns>
    /// <exception cref="McpException">No job has that handle.</exception>
    public JobInfo Get(string jobId) => Require(jobId).Describe();

    /// <summary>Answers every job this server still remembers.</summary>
    /// <returns>The jobs, most recently started first.</returns>
    public IReadOnlyList<JobInfo> List() =>
        [.. _jobs.Values.OrderByDescending(job => job.StartedAt).Select(job => job.Describe())];

    /// <summary>Reads the cursor a job's output was last collected from.</summary>
    /// <param name="jobId">The handle.</param>
    /// <returns>The cursor, or null when nothing has been collected yet.</returns>
    public string? CursorFor(string jobId) => Require(jobId).Cursor;

    /// <summary>Records where a job's output has now been collected to.</summary>
    /// <param name="jobId">The handle.</param>
    /// <param name="cursor">The new cursor.</param>
    public void Advance(string jobId, string cursor) => Require(jobId).Cursor = cursor;

    /// <summary>Interrupts a job and stops watching it.</summary>
    /// <param name="pane">The pane it runs in.</param>
    /// <param name="jobId">The handle.</param>
    /// <param name="cancellationToken">Cancels sending the interrupt.</param>
    /// <returns>The job.</returns>
    /// <remarks>
    /// Interrupting means sending the pane a <c>C-c</c>, which is a request
    /// rather than a guarantee: a program that ignores it keeps running, and
    /// the job then ends up reported as cancelled while the pane is still busy.
    /// The pane's current command is the honest check.
    /// </remarks>
    public async Task<JobInfo> CancelAsync(
        Pane pane,
        string jobId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pane);
        Job job = Require(jobId);
        if (job.State == JobState.Running)
        {
            await pane.SendKeysAsync(
                    new SendKeysRequest(text: "C-c", enter: false, literal: false),
                    cancellationToken)
                .ConfigureAwait(false);
            job.Finish(JobState.Cancelled, null);
        }

        return job.Describe();
    }

    private Job Require(string jobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        if (_jobs.TryGetValue(jobId.Trim(), out Job? job))
        {
            return job;
        }

        throw new McpException(
            $"No job '{jobId}' exists. Call tmux_list_jobs to see which do. A job is "
            + "forgotten when this server restarts, though the command itself keeps "
            + "running in its pane.");
    }

    private void Forget()
    {
        if (_jobs.Count < MaxRetained)
        {
            return;
        }

        // Only finished jobs are dropped: a running one still has a result
        // somebody is waiting for.
        foreach (Job stale in _jobs.Values
            .Where(job => job.State != JobState.Running)
            .OrderBy(job => job.EndedAt ?? job.StartedAt)
            .Take(_jobs.Count - MaxRetained + 1))
        {
            _jobs.TryRemove(stale.JobId, out _);
        }
    }

    private async Task WatchAsync(Server server, Pane pane, Job job)
    {
        try
        {
            await server.WaitForAsync(
                    new WaitForRequest(job.Token.Channel, TmuxWaitMode.Wait),
                    _shutdown.Token)
                .ConfigureAwait(false);

            int? status = await WriteTools
                .ReadStatusAsync(pane, job.Token, _shutdown.Token)
                .ConfigureAwait(false);
            job.Finish(JobState.Exited, status);
        }
        catch (OperationCanceledException)
        {
            // Shutdown. The command is tmux's and carries on without us.
        }
        catch (LibTmuxException)
        {
            job.Finish(JobState.Lost, null);
        }

        if (_logger is not null)
        {
            Log.JobEnded(_logger, job.JobId, job.PaneId, job.State);
        }
    }

    private sealed class Job(
        string jobId,
        string paneId,
        string command,
        WriteTools.RunToken token)
    {
        internal string JobId { get; } = jobId;

        internal string PaneId { get; } = paneId;

        internal WriteTools.RunToken Token { get; } = token;

        internal DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

        internal DateTimeOffset? EndedAt { get; private set; }

        internal JobState State { get; private set; } = JobState.Running;

        internal int? ExitStatus { get; private set; }

        internal string? Cursor { get; set; }

        internal Task? Watcher { get; set; }

        internal void Finish(JobState state, int? exitStatus)
        {
            if (State != JobState.Running)
            {
                return;
            }

            State = state;
            ExitStatus = exitStatus;
            EndedAt = DateTimeOffset.UtcNow;
        }

        internal JobInfo Describe() => new(
            JobId: JobId,
            PaneId: PaneId,
            Command: command,
            State: State,
            ExitStatus: ExitStatus,
            StartedAt: StartedAt,
            EndedAt: EndedAt,
            ElapsedSeconds: Math.Round(
                ((EndedAt ?? DateTimeOffset.UtcNow) - StartedAt).TotalSeconds,
                3));
    }
}
