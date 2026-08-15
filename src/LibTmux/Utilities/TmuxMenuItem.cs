namespace LibTmux;

/// <summary>One line of a tmux menu.</summary>
/// <remarks>
/// tmux takes menu items as bare triples on the command line rather than as
/// flags, so the order of the three is what tells them apart.
/// </remarks>
public sealed record TmuxMenuItem
{
    /// <summary>Initializes one menu item.</summary>
    /// <param name="name">The text shown for the item.</param>
    /// <param name="key">The key that chooses it.</param>
    /// <param name="command">The tmux command it runs.</param>
    public TmuxMenuItem(string name, string key, string command)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(command);
        Name = name;
        Key = key;
        Command = command;
    }

    /// <summary>Gets the text shown for the item.</summary>
    public string Name { get; }

    /// <summary>Gets the key that chooses it.</summary>
    public string Key { get; }

    /// <summary>Gets the tmux command it runs.</summary>
    public string Command { get; }
}
