using System.Runtime.Versioning;

namespace LibTmux;

/// <summary>Runs tmux-side filters and returns the surviving objects.</summary>
/// <remarks>
/// These take a raw tmux filter rather than a translated document. tmux
/// evaluates the text itself, so the closed field catalog does not apply and a
/// malformed token yields no rows rather than an error. Unlike the lenient
/// listings, a failed search throws: a caller who asked a question deserves to
/// know it was not answered.
/// </remarks>
public sealed partial class Server
{
    /// <summary>Runs a tmux-side filter over every session.</summary>
    /// <param name="filter">The raw tmux filter expression.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The sessions tmux kept.</returns>
    [UnsupportedOSPlatform("windows")]
    public Task<IReadOnlyList<Session>> SearchSessionsAsync(
        UnsafeTmuxFilter filter,
        CancellationToken cancellationToken = default) =>
        SearchAsync(
            "list-sessions",
            [],
            filter,
            static (owner, row) => RelationReader.ToSession(owner, row),
            cancellationToken);

    /// <summary>Runs a tmux-side filter over every window.</summary>
    /// <param name="filter">The raw tmux filter expression.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The windows tmux kept.</returns>
    [UnsupportedOSPlatform("windows")]
    public Task<IReadOnlyList<Window>> SearchWindowsAsync(
        UnsafeTmuxFilter filter,
        CancellationToken cancellationToken = default) =>
        SearchAsync(
            "list-windows",
            ["-a"],
            filter,
            static (owner, row) => RelationReader.ToWindow(owner, row),
            cancellationToken);

    /// <summary>Runs a tmux-side filter over every pane.</summary>
    /// <param name="filter">The raw tmux filter expression.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The panes tmux kept.</returns>
    [UnsupportedOSPlatform("windows")]
    public Task<IReadOnlyList<Pane>> SearchPanesAsync(
        UnsafeTmuxFilter filter,
        CancellationToken cancellationToken = default) =>
        SearchAsync(
            "list-panes",
            ["-a"],
            filter,
            static (owner, row) => RelationReader.ToPane(owner, row),
            cancellationToken);

    [UnsupportedOSPlatform("windows")]
    private async Task<IReadOnlyList<T>> SearchAsync<T>(
        string listCommand,
        IReadOnlyList<string> scope,
        UnsafeTmuxFilter filter,
        Func<Server, IReadOnlyDictionary<string, string?>, T> project,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = await RelationReader
            .ListAsync(this, listCommand, [.. scope, "-f", filter.Value], cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(row => project(this, row))];
    }
}
