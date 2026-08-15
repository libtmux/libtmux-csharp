namespace LibTmux;

/// <summary>Describes one <c>show-hooks</c> invocation.</summary>
public sealed record ListHooksRequest
{
    /// <summary>Initializes a request for every hook in a scope.</summary>
    /// <param name="scope">The scope to read, or null for the owner's own.</param>
    /// <param name="global">Whether the global table is read instead of the local one.</param>
    public ListHooksRequest(OptionScope? scope = null, bool global = false)
    {
        Scope = scope;
        Global = global;
    }

    /// <summary>Gets the scope to read, or null for the owner's own.</summary>
    public OptionScope? Scope { get; }

    /// <summary>Gets whether the global table is read instead of the local one.</summary>
    public bool Global { get; }
}
