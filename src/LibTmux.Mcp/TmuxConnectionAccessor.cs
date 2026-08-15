using System.Runtime.Versioning;

namespace LibTmux.Mcp;

/// <summary>Holds the one server every tool in this process talks to.</summary>
/// <remarks>
/// An assistant asks one thing at a time but over a long conversation, so the
/// connection outlives any single tool call. Discovering it once also means a
/// caller can point the server at a socket without every tool taking one.
/// </remarks>
[UnsupportedOSPlatform("windows")]
public sealed class TmuxConnectionAccessor : IDisposable
{
    private readonly ServerConnectionOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Server? _server;

    /// <summary>Initializes the accessor.</summary>
    /// <param name="options">How to reach tmux, or null for the ambient server.</param>
    public TmuxConnectionAccessor(ServerConnectionOptions? options = null) =>
        _options = options ?? new ServerConnectionOptions();

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    /// <summary>Answers the server, connecting on the first ask.</summary>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The server.</returns>
    public async Task<Server> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_server is Server connected)
        {
            return connected;
        }

        // Two tool calls can arrive together, and connecting twice would leave
        // one of them talking to a handle nothing else knows about.
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _server ??= await Server.ConnectAsync(_options, cancellationToken).ConfigureAwait(false);
            return _server;
        }
        finally
        {
            _gate.Release();
        }
    }
}
