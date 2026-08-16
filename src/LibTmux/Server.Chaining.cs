using System.Runtime.Versioning;
using LibTmux.Internal;

namespace LibTmux;

/// <summary>Starts a chain of commands on this server.</summary>
public sealed partial class Server
{
    /// <summary>Begins a chain that runs its commands in one tmux invocation.</summary>
    /// <returns>An empty chain bound to this server.</returns>
    /// <remarks>
    /// This is the batched counterpart to the one-shot methods. Which one is
    /// in use is visible where the call starts rather than in an option, and
    /// nothing runs until the chain is executed.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The handle has no connection.</exception>
    [UnsupportedOSPlatform("windows")]
    public TmuxChain Chain()
    {
        TmuxConnection connection = _connection
            ?? throw new InvalidOperationException("The server handle has no connection.");
        // The chain starts ungenerationed, because a chain of raw or
        // name-targeted commands has nothing to guard. Adding a command built
        // from an entity is what supplies a generation, and this is how the
        // chain then reaches a dispatcher that checks it.
        return new TmuxChain(
            connection.ServerDispatcher,
            [],
            connection.ExecuteGuardedGroupAsync);
    }
}
