namespace LibTmux;

/// <summary>One tmux command and the arguments it carries.</summary>
/// <param name="Name">The tmux command name, such as <c>new-window</c>.</param>
/// <param name="Arguments">Its arguments, separated as tmux will receive them.</param>
/// <remarks>
/// The typed methods on <see cref="Server" />, <see cref="Session" />,
/// <see cref="Window" />, and <see cref="Pane" /> each run one command and
/// return what it produced. This is that same command as a value, so several
/// can be handed to tmux together through <see cref="TmuxChain" /> rather than
/// one process at a time.
/// </remarks>
public sealed record TmuxCommand(string Name, IReadOnlyList<string> Arguments)
{
    /// <summary>Creates a command from its name and arguments.</summary>
    /// <param name="name">The tmux command name.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>The command.</returns>
    /// <exception cref="ArgumentException"><paramref name="name" /> is empty.</exception>
    public static TmuxCommand Create(string name, params string[] arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(arguments);
        return new TmuxCommand(name, [.. arguments]);
    }

    /// <summary>Gets the server generation this command's target belongs to.</summary>
    /// <remarks>
    /// A tmux ID such as <c>%2</c> is only meaningful on the server that issued
    /// it; a restarted server reuses those IDs for different objects. A command
    /// built from an entity therefore records which server the entity was read
    /// from, and <see cref="TmuxChain.ExecuteAsync" /> refuses to run it against
    /// a different one.
    ///
    /// Null means the command names no entity -- a raw command, or one whose
    /// target is a name rather than an ID -- and carries no such requirement.
    /// </remarks>
    public ServerGeneration? RequiredGeneration { get; init; }

    /// <summary>Returns this command the way tmux receives it.</summary>
    /// <returns>The command name followed by its arguments.</returns>
    public IReadOnlyList<string> ToArguments() => [Name, .. Arguments];
}
