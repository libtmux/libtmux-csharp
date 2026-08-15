using System.Runtime.Versioning;

namespace LibTmux;

/// <summary>Moves between a session's windows and owns ones it creates.</summary>
public sealed partial class Session
{
    /// <summary>Selects the window that was last active.</summary>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The window that is active afterwards.</returns>
    [UnsupportedOSPlatform("windows")]
    public async Task<Window> SelectLastWindowAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(["last-window", "-t", _id.ToString()], cancellationToken)
            .ConfigureAwait(false);
        return await ActiveWindowAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates a window in this session and takes ownership of it.</summary>
    /// <param name="request">The window to create.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>A scope that stops the window when disposed.</returns>
    [UnsupportedOSPlatform("windows")]
    public async Task<OwnedWindowScope> CreateOwnedWindowAsync(
        NewWindowRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        Window created = await CreateWindowAsync(request, cancellationToken).ConfigureAwait(false);
        return new OwnedWindowScope(created);
    }
}

/// <summary>Owns a window and stops it when disposed.</summary>
public sealed class OwnedWindowScope : IAsyncDisposable
{
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);
    private int _disposed;

    internal OwnedWindowScope(Window value) => Value = value;

    /// <summary>Gets the owned window.</summary>
    public Window Value { get; }

    /// <summary>Stops the owned window.</summary>
    /// <returns>A task that completes once the window is gone.</returns>
    /// <exception cref="LibTmuxException">The window could not be stopped.</exception>
    [UnsupportedOSPlatform("windows")]
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Teardown does not inherit the caller's token, because a canceled
        // caller still needs its window gone; it bounds itself instead so a
        // wedged socket cannot hang disposal forever.
        using CancellationTokenSource cleanup = new(CleanupTimeout);
        try
        {
            await Value.KillAsync(cancellationToken: cleanup.Token).ConfigureAwait(false);
        }
        catch (TmuxCommandException error) when (NamesAbsentWindow(error.Result))
        {
            // A window the server already dropped is the outcome that was asked
            // for. Anything else is surfaced: disposal that quietly fails to
            // clean up leaves a live window behind.
        }
    }

    private static bool NamesAbsentWindow(TmuxCommandResult result) =>
        result.StandardErrorLines.Any(static line =>
            line.Contains("can't find window", StringComparison.Ordinal)
            || line.Contains("no server running", StringComparison.Ordinal));
}
