namespace LibTmux;

/// <summary>Identifies one tmux daemon generation.</summary>
public readonly record struct ServerGeneration
{
    /// <summary>Initializes a server generation.</summary>
    public ServerGeneration(int processId, long startTime)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(startTime);
        ProcessId = processId;
        StartTime = startTime;
    }

    /// <summary>Gets the tmux daemon process identifier.</summary>
    public int ProcessId { get; }

    /// <summary>Gets the tmux daemon start time.</summary>
    public long StartTime { get; }
}
