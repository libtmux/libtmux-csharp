namespace LibTmux;

/// <summary>Describes one <c>confirm-before</c> invocation.</summary>
public sealed record ConfirmBeforeRequest
{
    private readonly string[] _command;

    /// <summary>Initializes a confirmation.</summary>
    /// <param name="command">The tmux command to run once confirmed.</param>
    /// <param name="prompt">The question shown, or null for tmux's own wording.</param>
    /// <param name="confirmKey">The key that confirms, or null for tmux's default.</param>
    /// <param name="defaultYes">Whether pressing enter confirms rather than cancels.</param>
    /// <param name="targetClient">The client to ask, or null for the caller's own.</param>
    public ConfirmBeforeRequest(
        IReadOnlyList<string> command,
        string? prompt = null,
        string? confirmKey = null,
        bool defaultYes = false,
        string? targetClient = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Count == 0)
        {
            throw new ArgumentException("A confirmation needs a command.", nameof(command));
        }

        _command = [.. command];
        Prompt = prompt;
        ConfirmKey = confirmKey;
        DefaultYes = defaultYes;
        TargetClient = targetClient;
    }

    /// <summary>Gets the tmux command to run once confirmed.</summary>
    public IReadOnlyList<string> Command => _command;

    /// <summary>Gets the question shown, or null for tmux's own wording.</summary>
    public string? Prompt { get; }

    /// <summary>Gets the key that confirms, or null for tmux's default.</summary>
    public string? ConfirmKey { get; }

    /// <summary>Gets whether pressing enter confirms rather than cancels.</summary>
    public bool DefaultYes { get; }

    /// <summary>Gets the client to ask, or null for the caller's own.</summary>
    public string? TargetClient { get; }
}
