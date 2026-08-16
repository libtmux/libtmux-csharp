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
/// <param name="Command">What was run.</param>
/// <param name="State">Where it has got to.</param>
/// <param name="ExitStatus">What it exited with, once it has.</param>
/// <param name="StartedAt">When it was started.</param>
/// <param name="EndedAt">When it finished, once it has.</param>
/// <param name="ElapsedSeconds">How long it has been running, or ran for.</param>
/// <remarks>
/// Starting one costs a single call and returns at once, so a command that
/// takes ten minutes does not spend ten minutes of the model's turn. The pane
/// keeps running it either way; the job is what makes the result collectable.
/// </remarks>
public sealed record JobInfo(
    string JobId,
    string PaneId,
    string Command,
    JobState State,
    int? ExitStatus,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    double ElapsedSeconds);

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
