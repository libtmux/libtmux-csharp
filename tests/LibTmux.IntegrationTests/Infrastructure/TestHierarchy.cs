using System.Runtime.Versioning;

namespace LibTmux.IntegrationTests.Infrastructure;

/// <summary>Reaches the hierarchy a test arranged, without believing an empty answer.</summary>
/// <remarks>
/// List accessors are lenient by contract: a listing that fails for any reason
/// answers empty rather than throwing. That is right for callers who want "what
/// is there", and wrong for a test arranging a server it just started, where an
/// empty answer under load is a failed command rather than an empty server.
/// Indexing straight into one turns that into an index error naming nothing.
/// </remarks>
[UnsupportedOSPlatform("windows")]
internal static class TestHierarchy
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    internal static Task<Session> RequireFirstSessionAsync(
        Server server,
        CancellationToken cancellationToken) =>
        RequireFirstAsync(
            () => server.GetSessionsAsync(cancellationToken),
            "sessions",
            cancellationToken);

    internal static Task<Window> RequireFirstWindowAsync(
        Session session,
        CancellationToken cancellationToken) =>
        RequireFirstAsync(
            () => session.GetWindowsAsync(cancellationToken),
            "windows",
            cancellationToken);

    internal static Task<Pane> RequireFirstPaneAsync(
        Window window,
        CancellationToken cancellationToken) =>
        RequireFirstAsync(
            () => window.GetPanesAsync(cancellationToken),
            "panes",
            cancellationToken);

    private static async Task<T> RequireFirstAsync<T>(
        Func<Task<IReadOnlyList<T>>> read,
        string relation,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            IReadOnlyList<T> items = await read().ConfigureAwait(false);
            if (items.Count > 0)
            {
                return items[0];
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new InvalidOperationException(
                    $"tmux reported no {relation} for a hierarchy this test arranged. "
                        + "A lenient listing answers empty when its command fails, so "
                        + "this is either a failed command or a server that lost what "
                        + "was built on it.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
