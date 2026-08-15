using System.Runtime.Versioning;
using LibTmux.Internal;

namespace LibTmux;

/// <summary>Provides raw command execution for a tmux server endpoint.</summary>
public sealed partial class Server
{
    private readonly TmuxCommandDispatcher _commandDispatcher;

    internal Server(TmuxCommandDispatcher commandDispatcher)
    {
        _commandDispatcher = commandDispatcher
            ?? throw new ArgumentNullException(nameof(commandDispatcher));
    }

    /// <summary>Executes one raw tmux command.</summary>
    [UnsupportedOSPlatform("windows")]
    public Task<TmuxCommandResult> ExecuteCommandAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        PlatformGuard.ThrowIfWindows();
        return _commandDispatcher.ExecuteAsync(arguments, cancellationToken);
    }
}
