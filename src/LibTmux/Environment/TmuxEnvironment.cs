using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace LibTmux.Internal;

/// <summary>Where the <c>TMUX</c> variable says a server is.</summary>
/// <remarks>
/// tmux exports <c>TMUX</c> into every pane it spawns as
/// <c>"&lt;socket-path&gt;,&lt;server-pid&gt;,&lt;session-id&gt;"</c>. Only the
/// socket path stays true for the pane's lifetime: the pid and session id are
/// frozen at spawn, and the session id goes stale as soon as the pane's window
/// moves between sessions.
/// </remarks>
/// <param name="SocketPath">The server socket the pane is attached to.</param>
/// <param name="ServerProcessId">The server pid recorded at pane spawn.</param>
/// <param name="SessionId">The session id recorded at pane spawn.</param>
internal sealed record TmuxServerLocation(
    string SocketPath,
    int ServerProcessId,
    SessionId SessionId);

/// <summary>Reads the variables tmux exports into the panes it spawns.</summary>
internal static class TmuxEnvironmentVariables
{
    /// <summary>The variable tmux exports into every pane it spawns.</summary>
    internal const string ServerVariable = "TMUX";

    /// <summary>The variable naming the pane a process was spawned in.</summary>
    internal const string PaneVariable = "TMUX_PANE";

    /// <summary>Tries to read the tmux server entry from an environment.</summary>
    /// <param name="environment">The environment, or null for the process.</param>
    /// <param name="entry">The parsed entry when present and well formed.</param>
    /// <returns>True when the environment names a tmux server.</returns>
    internal static bool TryRead(
        IReadOnlyDictionary<string, string>? environment,
        [NotNullWhen(true)] out TmuxServerLocation? entry)
    {
        entry = null;
        string? value = Read(environment, ServerVariable);
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        string[] parts = value.Split(',');
        if (parts.Length != 3
            || parts[0].Length == 0
            || !int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int processId)
            || !SessionId.TryParse($"${parts[2]}", out SessionId sessionId))
        {
            return false;
        }

        entry = new TmuxServerLocation(parts[0], processId, sessionId);
        return true;
    }

    /// <summary>Reads the pane a process was spawned in.</summary>
    /// <param name="environment">The environment, or null for the process.</param>
    /// <param name="paneId">The parsed pane identifier.</param>
    /// <returns>True when the environment names a pane.</returns>
    internal static bool TryReadPane(
        IReadOnlyDictionary<string, string>? environment,
        out PaneId paneId) =>
        PaneId.TryParse(Read(environment, PaneVariable), out paneId);

    private static string? Read(
        IReadOnlyDictionary<string, string>? environment,
        string name) =>
        environment is null
            ? System.Environment.GetEnvironmentVariable(name)
            : environment.TryGetValue(name, out string? value)
                ? value
                : null;
}
