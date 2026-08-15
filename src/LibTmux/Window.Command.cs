using System.Runtime.Versioning;
using LibTmux.Internal;

namespace LibTmux;

/// <summary>Provides raw command execution for a tmux window.</summary>
public sealed partial class Window
{
    private readonly TmuxCommandDispatcher _commandDispatcher;
    private readonly string _defaultTarget;

    internal Window(TmuxCommandDispatcher commandDispatcher, string defaultTarget)
    {
        _commandDispatcher = commandDispatcher
            ?? throw new ArgumentNullException(nameof(commandDispatcher));
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultTarget);
        _defaultTarget = defaultTarget;
    }

    /// <summary>Executes one raw tmux command against this window.</summary>
    [UnsupportedOSPlatform("windows")]
    public Task<TmuxCommandResult> ExecuteCommandAsync(
        IReadOnlyList<string> arguments,
        string? targetOverride = null,
        CancellationToken cancellationToken = default)
    {
        PlatformGuard.ThrowIfWindows();
        return TargetedCommandArguments.ExecuteAsync(
            _commandDispatcher,
            arguments,
            targetOverride ?? _defaultTarget,
            cancellationToken);
    }
}
