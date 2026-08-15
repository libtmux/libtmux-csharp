namespace LibTmux;

/// <summary>What to do with a <c>wait-for</c> channel.</summary>
public enum TmuxWaitMode
{
    /// <summary>Block until the channel is signalled.</summary>
    Wait,

    /// <summary>Release everything waiting on the channel.</summary>
    Signal,

    /// <summary>Take the channel's lock, blocking until it is free.</summary>
    Lock,

    /// <summary>Release the channel's lock.</summary>
    Unlock,
}

/// <summary>Describes one <c>wait-for</c> invocation.</summary>
/// <remarks>
/// tmux channels let a command wait for something another command will do.
/// Waiting blocks the tmux client, so a call that waits has nothing to answer
/// until whoever it is waiting for arrives.
/// </remarks>
public sealed record WaitForRequest
{
    /// <summary>Initializes a channel request.</summary>
    /// <param name="channel">The channel name.</param>
    /// <param name="mode">What to do with it.</param>
    public WaitForRequest(string channel, TmuxWaitMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        Channel = channel;
        Mode = mode;
    }

    /// <summary>Gets the channel name.</summary>
    public string Channel { get; }

    /// <summary>Gets what to do with it.</summary>
    public TmuxWaitMode Mode { get; }
}
