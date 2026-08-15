namespace LibTmux;

/// <summary>Describes one <c>show-options</c> invocation for every option in a scope.</summary>
public sealed record GetOptionsRequest
{
    /// <summary>Initializes a request for every option in a scope.</summary>
    /// <param name="scope">The scope to read in, or null for the owner's own.</param>
    /// <param name="global">Whether the global table is read instead of the local one.</param>
    /// <param name="includeHooks">Whether hooks are listed alongside options.</param>
    /// <param name="includeInherited">Whether values inherited from a parent scope are included.</param>
    /// <param name="quiet">Whether an empty table is answered with nothing instead of an error.</param>
    public GetOptionsRequest(
        OptionScope? scope = null,
        bool global = false,
        bool includeHooks = false,
        bool includeInherited = false,
        bool quiet = false)
    {
        Scope = scope;
        Global = global;
        IncludeHooks = includeHooks;
        IncludeInherited = includeInherited;
        Quiet = quiet;
    }

    /// <summary>Gets the scope to read in, or null for the owner's own.</summary>
    public OptionScope? Scope { get; }

    /// <summary>Gets whether the global table is read instead of the local one.</summary>
    public bool Global { get; }

    /// <summary>Gets whether hooks are listed alongside options.</summary>
    public bool IncludeHooks { get; }

    /// <summary>Gets whether values inherited from a parent scope are included.</summary>
    public bool IncludeInherited { get; }

    /// <summary>Gets whether an empty table is answered with nothing instead of an error.</summary>
    public bool Quiet { get; }
}
