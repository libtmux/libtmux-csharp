namespace LibTmux;

/// <summary>One tmux paste buffer.</summary>
/// <remarks>
/// tmux lists a buffer's size and the start of its contents, not the whole
/// thing. Reading a buffer in full is a separate command, so what is listed is
/// enough to choose between buffers rather than to use one.
/// </remarks>
public sealed record TmuxBuffer
{
    /// <summary>Initializes one buffer.</summary>
    /// <param name="name">The buffer name.</param>
    /// <param name="size">How many bytes it holds.</param>
    /// <param name="sample">The start of its contents, as tmux chose to show it.</param>
    public TmuxBuffer(string name, long size, string? sample)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Size = size;
        Sample = sample;
    }

    /// <summary>Gets the buffer name.</summary>
    public string Name { get; }

    /// <summary>Gets how many bytes it holds.</summary>
    public long Size { get; }

    /// <summary>Gets the start of its contents, as tmux chose to show it.</summary>
    public string? Sample { get; }
}
