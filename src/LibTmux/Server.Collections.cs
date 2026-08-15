using System.Runtime.Versioning;

namespace LibTmux;

/// <summary>Reads server-wide collections of tmux objects.</summary>
/// <remarks>
/// Leniency differs per accessor and is not a style choice. Session listings
/// answer "what is there", so any failure reads as nothing there. Window and
/// pane listings answer a narrower question, so only an absent daemon or
/// socket reads as empty and a real tmux error still surfaces.
/// </remarks>
public sealed partial class Server
{
    /// <summary>Reads every session on this server.</summary>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The sessions, empty when the listing fails.</returns>
    [UnsupportedOSPlatform("windows")]
    public Task<IReadOnlyList<Session>> GetSessionsAsync(
        CancellationToken cancellationToken = default) =>
        ListAsync(
            "list-sessions",
            [],
            static (owner, row) => RelationReader.ToSession(owner, row),
            LenientListPolicy.AnyFailure,
            cancellationToken);

    /// <summary>Reads every session with at least one attached client.</summary>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The attached sessions, empty when the listing fails.</returns>
    [UnsupportedOSPlatform("windows")]
    public async Task<IReadOnlyList<Session>> GetAttachedSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = await ListRowsAsync(
                "list-sessions",
                [],
                LenientListPolicy.AnyFailure,
                cancellationToken)
            .ConfigureAwait(false);
        return
        [
            .. rows
                .Where(static row => row.TryGetValue("session_attached", out string? value)
                    && value is not null
                    && value != "0")
                .Select(row => RelationReader.ToSession(this, row)),
        ];
    }

    /// <summary>Reads every window on this server.</summary>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The windows, empty when no daemon or socket is present.</returns>
    [UnsupportedOSPlatform("windows")]
    public Task<IReadOnlyList<Window>> GetWindowsAsync(
        CancellationToken cancellationToken = default) =>
        ListAsync(
            "list-windows",
            ["-a"],
            static (owner, row) => RelationReader.ToWindow(owner, row),
            LenientListPolicy.MissingDaemonOrSocket,
            cancellationToken);

    /// <summary>Reads every pane on this server.</summary>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The panes, empty when no daemon or socket is present.</returns>
    [UnsupportedOSPlatform("windows")]
    public Task<IReadOnlyList<Pane>> GetPanesAsync(
        CancellationToken cancellationToken = default) =>
        ListAsync(
            "list-panes",
            ["-a"],
            static (owner, row) => RelationReader.ToPane(owner, row),
            LenientListPolicy.MissingDaemonOrSocket,
            cancellationToken);

    [UnsupportedOSPlatform("windows")]
    private async Task<IReadOnlyList<T>> ListAsync<T>(
        string listCommand,
        IReadOnlyList<string> extraArguments,
        Func<Server, IReadOnlyDictionary<string, string?>, T> project,
        LenientListPolicy policy,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows =
            await ListRowsAsync(listCommand, extraArguments, policy, cancellationToken)
                .ConfigureAwait(false);
        return [.. rows.Select(row => project(this, row))];
    }

    [UnsupportedOSPlatform("windows")]
    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> ListRowsAsync(
        string listCommand,
        IReadOnlyList<string> extraArguments,
        LenientListPolicy policy,
        CancellationToken cancellationToken)
    {
        try
        {
            return await RelationReader
                .ListAsync(this, listCommand, extraArguments, cancellationToken)
                .ConfigureAwait(false);
        }
        // Leniency is for a server that is not there: Python libtmux answers an
        // empty list when the daemon or socket is missing, and these accessors
        // keep that. It is deliberately not extended to a handle that cannot
        // answer — "the server reported no tmux version" is an endpoint that
        // has read nothing, and an empty list for that is a wrong answer
        // wearing the shape of a real one.
        catch (LibTmuxException error) when (policy.Tolerates(error))
        {
            return [];
        }
    }

    private sealed class LenientListPolicy
    {
        private readonly bool _anyFailure;

        private LenientListPolicy(bool anyFailure) => _anyFailure = anyFailure;

        internal static LenientListPolicy AnyFailure { get; } = new(anyFailure: true);

        internal static LenientListPolicy MissingDaemonOrSocket { get; } =
            new(anyFailure: false);

        internal bool Tolerates(LibTmuxException error) =>
            _anyFailure || IsMissingDaemonOrSocket(error);

        private static bool IsMissingDaemonOrSocket(LibTmuxException error) =>
            error is TmuxCommandNotFoundException
            || (error is TmuxCommandException command
                && command.Result.StandardErrorLines.Any(static line =>
                    line.Contains("no server running", StringComparison.Ordinal)
                    || line.Contains("error connecting to", StringComparison.Ordinal)
                    || line.Contains("No such file or directory", StringComparison.Ordinal)));
    }
}
