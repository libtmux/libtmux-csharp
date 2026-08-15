using System.Runtime.Versioning;
using LibTmux.Internal;

namespace LibTmux;

/// <summary>Commands tmux runs together, in one process.</summary>
/// <remarks>
/// <para>
/// A one-shot call starts a tmux client, runs one command, and lets it exit,
/// which is the right shape for one command and the wrong shape for fifty: the
/// process start dominates. A chain hands tmux the whole sequence at once, so
/// the cost is paid once no matter how many commands are in it.
/// </para>
/// <para>
/// Building a chain reaches nothing; only <see cref="ExecuteAsync" /> does.
/// Each step returns a new chain, so a partly built one can be shared without
/// another caller's additions appearing in it.
/// </para>
/// </remarks>
[UnsupportedOSPlatform("windows")]
public sealed class TmuxChain
{
    private readonly TmuxCommandDispatcher _dispatcher;
    private readonly IReadOnlyList<TmuxCommand> _commands;

    internal TmuxChain(TmuxCommandDispatcher dispatcher, IReadOnlyList<TmuxCommand> commands)
    {
        _dispatcher = dispatcher;
        _commands = commands;
    }

    /// <summary>Gets the commands this chain will run, in order.</summary>
    public IReadOnlyList<TmuxCommand> Commands => _commands;

    /// <summary>Adds one command and returns the longer chain.</summary>
    /// <param name="command">The command to run after the ones already added.</param>
    /// <returns>A chain ending with <paramref name="command" />.</returns>
    public TmuxChain Then(TmuxCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new TmuxChain(_dispatcher, [.. _commands, command]);
    }

    /// <summary>Adds one command by name and returns the longer chain.</summary>
    /// <param name="name">The tmux command name.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>A chain ending with the named command.</returns>
    public TmuxChain Then(string name, params string[] arguments) =>
        Then(TmuxCommand.Create(name, arguments));

    /// <summary>Runs every command in one tmux invocation.</summary>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What that one invocation produced.</returns>
    /// <remarks>
    /// tmux runs the commands in order and prints their output as one stream,
    /// so the result belongs to the chain rather than to any single command.
    /// A command that fails stops the ones after it, which is tmux's own
    /// behavior for a grouped run rather than anything imposed here.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The chain has no commands.</exception>
    /// <exception cref="TmuxCommandException">tmux reported the run failed.</exception>
    [UnsupportedOSPlatform("windows")]
    public async Task<TmuxCommandResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        if (_commands.Count == 0)
        {
            throw new InvalidOperationException("A chain needs at least one command.");
        }

        TmuxCommandResult result = await _dispatcher
            .ExecuteGroupAsync(
                [.. _commands.Select(static command => command.ToArguments())],
                cancellationToken)
            .ConfigureAwait(false);
        TmuxCommandFailure.ThrowIfFailed(result, "chain");
        return result;
    }
}
