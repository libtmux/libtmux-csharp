namespace LibTmux;

/// <summary>Describes one <c>list-buffers</c> invocation.</summary>
public sealed record ListBuffersRequest
{
    /// <summary>Initializes a buffer listing.</summary>
    /// <param name="format">The tmux format each buffer is rendered with.</param>
    /// <param name="filter">A tmux filter expression, kept as written.</param>
    public ListBuffersRequest(string? format = null, UnsafeTmuxFilter? filter = null)
    {
        Format = format;
        Filter = filter;
    }

    /// <summary>Gets the tmux format each buffer is rendered with.</summary>
    public string? Format { get; }

    /// <summary>Gets the tmux filter expression, kept as written.</summary>
    public UnsafeTmuxFilter? Filter { get; }
}
