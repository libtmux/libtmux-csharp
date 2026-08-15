using System.Runtime.Versioning;

namespace LibTmux.Testing;

/// <summary>Creates a throwaway session for a test and stops it afterwards.</summary>
[UnsupportedOSPlatform("windows")]
public sealed class TemporarySessionScope : IAsyncDisposable
{
    private readonly OwnedSessionScope _owned;

    private TemporarySessionScope(OwnedSessionScope owned)
    {
        _owned = owned;
        Session = owned.Value;
    }

    /// <summary>Gets the temporary session.</summary>
    public Session Session { get; }

    /// <summary>Creates a temporary session on a running server.</summary>
    /// <param name="server">The server to create the session on.</param>
    /// <param name="request">The session to create.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>The scope owning the session.</returns>
    [UnsupportedOSPlatform("windows")]
    internal static async Task<TemporarySessionScope> StartAsync(
        Server server,
        NewSessionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        OwnedSessionScope owned = await server
            .CreateOwnedSessionAsync(request, cancellationToken)
            .ConfigureAwait(false);
        return new TemporarySessionScope(owned);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _owned.DisposeAsync();
}
