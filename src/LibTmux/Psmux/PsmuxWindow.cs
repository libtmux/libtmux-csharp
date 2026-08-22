#pragma warning disable CA1416

namespace LibTmux;

/// <summary>An immutable observation of one psmux window.</summary>
/// <remarks>
/// This observation is bound to <see cref="Server" />'s captured generation.
/// Query methods throw <see cref="StaleServerGenerationException" /> after replacement;
/// call <see cref="PsmuxServer.RefreshAsync" /> to obtain a fresh observation.
/// </remarks>
public sealed class PsmuxWindow
{
    private readonly Window _inner;

    internal PsmuxWindow(PsmuxServer server, Window inner)
    {
        Server = server;
        _inner = inner;
    }

    /// <summary>Gets the psmux endpoint that produced this observation.</summary>
    public PsmuxServer Server { get; }

    /// <summary>Gets the captured window identifier.</summary>
    public WindowId Id => _inner.Id;

    /// <summary>Gets the captured parent session identifier.</summary>
    public SessionId SessionId => _inner.EntityKey.SessionId;

    /// <summary>Gets the captured window index.</summary>
    public int Index => _inner.Index;

    /// <summary>Gets the captured window name.</summary>
    public string Name => _inner.Name;

    /// <summary>Gets the captured width in columns.</summary>
    public int Width => _inner.Width;

    /// <summary>Gets the captured height in rows.</summary>
    public int Height => _inner.Height;

    /// <summary>Reads the window's current panes.</summary>
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
    /// <exception cref="TmuxObjectNotFoundException">
    /// The observed window is no longer visible.
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
