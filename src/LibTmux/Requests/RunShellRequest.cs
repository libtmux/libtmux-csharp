namespace LibTmux;

/// <summary>Describes one <c>run-shell</c> invocation.</summary>
public sealed record RunShellRequest
{
    private readonly string[]? _arguments;

    /// <summary>Initializes a shell command.</summary>
    /// <param name="command">The command to run.</param>
    /// <param name="arguments">Arguments passed to it without a shell in between.</param>
    /// <param name="background">Whether tmux returns without waiting for it.</param>
    /// <param name="delay">How long tmux waits before starting it.</param>
    /// <param name="asTmuxCommand">Whether the text is a tmux command rather than a shell one.</param>
    /// <param name="targetPane">The pane the command runs against.</param>
    /// <param name="workingDirectory">The directory it starts in.</param>
    /// <param name="showStandardError">Whether its error output is shown too.</param>
    public RunShellRequest(
        string command,
        IReadOnlyList<string>? arguments = null,
        bool background = false,
        TimeSpan? delay = null,
        bool asTmuxCommand = false,
        string? targetPane = null,
        string? workingDirectory = null,
        bool showStandardError = false)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (delay is TimeSpan span && span < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delay),
                delay,
                "A delay cannot run backwards.");
        }

        Command = command;
        _arguments = arguments is null ? null : [.. arguments];
        Background = background;
        Delay = delay;
        AsTmuxCommand = asTmuxCommand;
        TargetPane = targetPane;
        WorkingDirectory = workingDirectory;
        ShowStandardError = showStandardError;
    }

    /// <summary>Gets the command to run.</summary>
    public string Command { get; }

    /// <summary>Gets arguments passed to it without a shell in between.</summary>
    public IReadOnlyList<string>? Arguments => _arguments;

    /// <summary>Gets whether tmux returns without waiting for it.</summary>
    public bool Background { get; }

    /// <summary>Gets how long tmux waits before starting it.</summary>
    public TimeSpan? Delay { get; }

    /// <summary>Gets whether the text is a tmux command rather than a shell one.</summary>
    public bool AsTmuxCommand { get; }

    /// <summary>Gets the pane the command runs against.</summary>
    public string? TargetPane { get; }

    /// <summary>Gets the directory it starts in.</summary>
    public string? WorkingDirectory { get; }

    /// <summary>Gets whether its error output is shown too.</summary>
    public bool ShowStandardError { get; }
}
