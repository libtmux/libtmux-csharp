namespace LibTmux.Mcp;

/// <summary>Where a background command has got to.</summary>
public enum JobState
{
    /// <summary>Still running.</summary>
    Running = 0,

    /// <summary>Finished, with an exit status.</summary>
    Exited = 1,

    /// <summary>The caller asked for it to stop.</summary>
    Cancelled = 2,

    /// <summary>The pane it ran in went away before it finished.</summary>
    Lost = 3,
}

/// <summary>A command that outlives the call that started it.</summary>
/// <param name="JobId">The handle to ask about it with.</param>
/// <param name="PaneId">The pane it runs in.</param>
/// <param name="SocketName">The named socket it runs on, or null for a socket path.</param>
/// <param name="SocketPath">The socket path it runs on, or null for a named socket.</param>
/// <param name="EndpointFingerprint">The exact socket endpoint, as a stable opaque digest.</param>
/// <param name="ServerGeneration">The tmux daemon that owned the pane when it started.</param>
/// <param name="CommandBytes">The UTF-8 size of the command, whose text is not retained.</param>
/// <param name="State">Where it has got to.</param>
/// <param name="ExitStatus">What it exited with, once it has.</param>
/// <param name="StartedAt">When it was started.</param>
/// <param name="EndedAt">When it finished, once it has.</param>
/// <param name="ElapsedSeconds">How long it has been running, or ran for.</param>
/// <remarks>
/// Starting one costs a single call and returns at once, so a command that
/// takes ten minutes does not spend ten minutes of the model's turn. The pane
/// keeps running it either way; the job is what makes the result collectable.
/// Command text is deliberately absent because shell commands commonly contain
/// credentials; the handle and endpoint are enough to collect or cancel it.
/// </remarks>
public sealed record JobInfo(
    string JobId,
    string PaneId,
    string? SocketName,
    string? SocketPath,
    string EndpointFingerprint,
    ServerGeneration ServerGeneration,
    int CommandBytes,
    JobState State,
    int? ExitStatus,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    double ElapsedSeconds);

/// <summary>A bounded inventory of background jobs.</summary>
/// <param name="Jobs">The jobs that fit, newest first.</param>
/// <param name="TotalJobs">How many jobs the server remembers.</param>
/// <param name="Truncated">Whether older jobs were omitted to fit the response budget.</param>
public sealed record JobList(
    IReadOnlyList<JobInfo> Jobs,
    int TotalJobs,
    bool Truncated);

/// <summary>A background command, and whatever it has printed since last asked.</summary>
/// <param name="Job">Where the command has got to.</param>
/// <param name="Output">
/// What the pane has printed since this job was last asked about. Each call
/// answers only what is new, so watching a long job does not re-read its whole
/// output every time.
/// </param>
/// <param name="LinesMissed">
/// Whether scrollback dropped lines nobody saw. A job printing faster than
/// <c>history-limit</c> holds loses the middle of its output for good.
/// </param>
public sealed record JobReport(JobInfo Job, BoundedText Output, bool LinesMissed);
