namespace LibTmux;

/// <summary>Describes one <c>unbind-key</c> invocation.</summary>
public sealed record UnbindKeyRequest
{
    /// <summary>Initializes a request to remove a binding.</summary>
    /// <param name="key">The key to unbind, or null when removing them all.</param>
    /// <param name="keyTable">The key table, or null for the prefix table.</param>
    /// <param name="all">Whether every binding in the table goes.</param>
    /// <param name="quiet">Whether an absent binding is passed over in silence.</param>
    public UnbindKeyRequest(
        string? key = null,
        string? keyTable = null,
        bool all = false,
        bool quiet = false)
    {
        if (!all && string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Removing one binding needs the key it is bound to.",
                nameof(key));
        }

        Key = key;
        KeyTable = keyTable;
        All = all;
        Quiet = quiet;
    }

    /// <summary>Gets the key to unbind, or null when removing them all.</summary>
    public string? Key { get; }

    /// <summary>Gets the key table, or null for the prefix table.</summary>
    public string? KeyTable { get; }

    /// <summary>Gets whether every binding in the table goes.</summary>
    public bool All { get; }

    /// <summary>Gets whether an absent binding is passed over in silence.</summary>
    public bool Quiet { get; }
}
