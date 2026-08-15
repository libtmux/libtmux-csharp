namespace LibTmux;

/// <summary>Describes one <c>bind-key</c> invocation.</summary>
public sealed record BindKeyRequest
{
    private readonly string[] _command;

    /// <summary>Initializes a key binding.</summary>
    /// <param name="key">The key to bind.</param>
    /// <param name="command">The tmux command and its arguments.</param>
    /// <param name="keyTable">The key table, or null for the prefix table.</param>
    /// <param name="note">A note describing the binding.</param>
    /// <param name="repeat">Whether the key may repeat without the prefix.</param>
    public BindKeyRequest(
        string key,
        IReadOnlyList<string> command,
        string? keyTable = null,
        string? note = null,
        bool repeat = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(command);
        if (command.Count == 0)
        {
            throw new ArgumentException("A binding needs a command.", nameof(command));
        }

        Key = key;

        // The request is read again at dispatch, so a caller that kept the list
        // could otherwise change the argv after constructing it.
        _command = [.. command];
        KeyTable = keyTable;
        Note = note;
        Repeat = repeat;
    }

    /// <summary>Gets the key to bind.</summary>
    public string Key { get; }

    /// <summary>Gets the tmux command and its arguments.</summary>
    public IReadOnlyList<string> Command => _command;

    /// <summary>Gets the key table, or null for the prefix table.</summary>
    public string? KeyTable { get; }

    /// <summary>Gets the note describing the binding.</summary>
    public string? Note { get; }

    /// <summary>Gets whether the key may repeat without the prefix.</summary>
    public bool Repeat { get; }
}
