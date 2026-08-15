using System.Runtime.Versioning;

namespace LibTmux.Testing;

/// <summary>Creates a throwaway window for a test and stops it afterwards.</summary>
[UnsupportedOSPlatform("windows")]
public sealed class TemporaryWindowScope : IAsyncDisposable
{
    private readonly OwnedWindowScope _owned;

    private TemporaryWindowScope(OwnedWindowScope owned)
    {
        _owned = owned;
        Window = owned.Value;
    }

    /// <summary>Gets the temporary window.</summary>
    public Window Window { get; }

    /// <summary>Creates a temporary window in a session.</summary>
    /// <param name="session">The session to create the window in.</param>
    /// <param name="request">The window to create.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>The scope owning the window.</returns>
    [UnsupportedOSPlatform("windows")]
    internal static async Task<TemporaryWindowScope> StartAsync(
        Session session,
        NewWindowRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        OwnedWindowScope owned = await session
            .CreateOwnedWindowAsync(request, cancellationToken)
            .ConfigureAwait(false);
        return new TemporaryWindowScope(owned);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _owned.DisposeAsync();
}
