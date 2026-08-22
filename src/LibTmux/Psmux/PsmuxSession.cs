#pragma warning disable CA1416

namespace LibTmux;

/// <summary>An immutable observation of the sole psmux session.</summary>
/// <remarks>
/// This observation is bound to <see cref="Server" />'s captured generation.
/// Query methods throw <see cref="StaleServerGenerationException" /> after replacement;
/// call <see cref="PsmuxServer.RefreshAsync" /> to obtain a fresh observation.
/// </remarks>
public sealed class PsmuxSession
{
    private readonly Session _inner;

    internal PsmuxSession(PsmuxServer server, Session inner)
    {
        Server = server;
        _inner = inner;
    }

    /// <summary>Gets the psmux endpoint that produced this observation.</summary>
    public PsmuxServer Server { get; }

    /// <summary>Gets the captured session identifier.</summary>
    public SessionId Id => _inner.Id;

    /// <summary>Gets the captured session name.</summary>
    public string Name => _inner.Name;

    /// <summary>Gets whether a client was attached when the session was read.</summary>
    public bool Attached => _inner.Attached;

    /// <summary>Reads the session's current windows.</summary>
    /// <param name="cancellationToken">Cancels the psmux query.</param>
    /// <returns>Current immutable window observations.</returns>
    /// <exception cref="InvalidOperationException">
    /// The selected namespace has no live session.
    /// </exception>
    /// <exception cref="StaleServerGenerationException">
    /// The sole session was replaced after this observation was read.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// The selected namespace contains more than one session.
    /// </exception>
    /// <exception cref="LibTmuxException">The verified client could not complete the query.</exception>
    public async Task<IReadOnlyList<PsmuxWindow>> GetWindowsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Window> windows = await _inner
            .GetWindowsAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. windows.Select(window => new PsmuxWindow(Server, window))];
    }

    /// <summary>Reads the session's current panes.</summary>
    /// <param name="cancellationToken">Cancels the psmux query.</param>
    /// <returns>Current immutable pane observations.</returns>
    /// <exception cref="InvalidOperationException">
    /// The selected namespace has no live session.
    /// </exception>
    /// <exception cref="StaleServerGenerationException">
    /// The sole session was replaced after this observation was read.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// The selected namespace contains more than one session.
    /// </exception>
    /// <exception cref="LibTmuxException">The verified client could not complete the query.</exception>
    public async Task<IReadOnlyList<PsmuxPane>> GetPanesAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Pane> panes = await _inner
            .GetPanesAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. panes.Select(pane => new PsmuxPane(Server, pane))];
    }
}

#pragma warning restore CA1416
