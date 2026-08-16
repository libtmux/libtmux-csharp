using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace LibTmux.Mcp;

/// <summary>Tells a waiter the moment a pane prints something.</summary>
/// <remarks>
/// <para>
/// tmux will report pane output as it happens to a client in control mode, so
/// a wait can sleep until there is something to look at instead of asking
/// every few milliseconds whether anything changed. On a wait that ends up
/// timing out, that is the difference between hundreds of tmux processes and
/// none.
/// </para>
/// <para>
/// What arrives on that stream is the pane's raw terminal bytes — escape
/// sequences, redraws and all — which is why it is used as a signal and never
/// as content. The text a caller gets always comes from a capture, which is
/// what tmux has already rendered.
/// </para>
/// <para>
/// A control client sees only the session it attached to, so watches are per
/// session and reference counted. Control mode is an optimisation, not a
/// requirement: when a client cannot start, waiting falls back to polling and
/// the caller cannot tell the difference except in cost.
/// </para>
/// </remarks>
[UnsupportedOSPlatform("windows")]
public sealed class PaneActivityHub : IAsyncDisposable
{
    /// <summary>How long a poll-based wait sleeps between reads.</summary>
    internal static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(60);

    private readonly ConcurrentDictionary<string, SessionWatch> _watches = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PaneSignal> _signals = new(StringComparer.Ordinal);
    private readonly ILogger? _logger;
    private bool _disposed;

    /// <summary>Initializes the hub.</summary>
    /// <param name="logger">Records why a control client could not start.</param>
    public PaneActivityHub(ILogger? logger = null) => _logger = logger;

    /// <summary>Gets whether any session is currently watched through control mode.</summary>
    public bool IsStreaming => !_watches.IsEmpty;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        foreach (KeyValuePair<string, SessionWatch> entry in _watches)
        {
            await entry.Value.DisposeAsync().ConfigureAwait(false);
        }

        _watches.Clear();
        _signals.Clear();
    }

    /// <summary>Watches a pane's session for as long as the result is held.</summary>
    /// <param name="pane">The pane whose session to watch.</param>
    /// <param name="cancellationToken">Cancels starting the control client.</param>
    /// <returns>A lease that stops watching when disposed.</returns>
    /// <remarks>
    /// Take one of these around a wait. Without it the wait still works, by
    /// polling; with it, tmux does the waiting.
    /// </remarks>
    public async Task<IAsyncDisposable> WatchAsync(Pane pane, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pane);
        if (_disposed)
        {
            return NullLease.Instance;
        }

        string sessionId = pane.Session.Id.ToString();
        SessionWatch watch = _watches.GetOrAdd(
            sessionId,
            key => new SessionWatch(key, this));

        bool started = await watch.EnsureStartedAsync(pane.Server, cancellationToken)
            .ConfigureAwait(false);
        if (!started)
        {
            _watches.TryRemove(sessionId, out _);
            return NullLease.Instance;
        }

        return watch.Lease();
    }

    /// <summary>Waits until a pane prints something, or the time runs out.</summary>
    /// <param name="paneId">The pane to wait on.</param>
    /// <param name="signalBefore">
    /// The signal captured before the caller last read the pane. Passing the
    /// one taken before the read is what stops output that arrived during it
    /// from being missed.
    /// </param>
    /// <param name="timeout">How long to wait at most.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns><see langword="true" /> when the pane printed something.</returns>
    public async Task<bool> WaitForActivityAsync(
        string paneId,
        object? signalBefore,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paneId);
        if (_disposed || timeout <= TimeSpan.Zero)
        {
            return false;
        }

        // Without a live control client there is nothing to be woken by, so the
        // caller sleeps a short fixed step and reads again.
        if (signalBefore is not Task wake)
        {
            TimeSpan step = timeout < PollInterval ? timeout : PollInterval;
            await Task.Delay(step, cancellationToken).ConfigureAwait(false);
            return false;
        }

        Task expiry = Task.Delay(timeout, cancellationToken);
        Task finished = await Task.WhenAny(wake, expiry).ConfigureAwait(false);
        if (finished == expiry)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }

        return true;
    }

    /// <summary>Takes the token that a later wait on this pane will wake from.</summary>
    /// <param name="paneId">The pane about to be read.</param>
    /// <returns>The token, or null when nothing is streaming this pane.</returns>
    /// <remarks>
    /// Take this <em>before</em> reading the pane. Output that arrives between
    /// the read and the wait would otherwise leave the waiter asleep with the
    /// answer already on screen.
    /// </remarks>
    public object? CaptureSignal(string paneId)
    {
        ArgumentNullException.ThrowIfNull(paneId);
        return IsStreaming
            ? _signals.GetOrAdd(paneId, _ => new PaneSignal()).Current
            : null;
    }

    private void OnPaneOutput(string paneId)
    {
        if (_signals.TryGetValue(paneId, out PaneSignal? signal))
        {
            signal.Fire();
        }
    }

    /// <summary>One pane's "something happened" bell.</summary>
    /// <remarks>
    /// The completion source is replaced rather than reset, so a waiter that
    /// took the previous one still completes: a bell nobody was listening for
    /// yet must not be lost.
    /// </remarks>
    private sealed class PaneSignal
    {
        private TaskCompletionSource _source =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Current => Volatile.Read(ref _source).Task;

        internal void Fire() =>
            Interlocked.Exchange(
                    ref _source,
                    new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
                .TrySetResult();
    }

    /// <summary>One session's control client, and how many waits need it.</summary>
    private sealed class SessionWatch(string sessionId, PaneActivityHub hub) : IAsyncDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private IControlModeSession? _session;
        private Task? _pump;
        private int _leases;

        internal async Task<bool> EnsureStartedAsync(
            Server server,
            CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_session is not null)
                {
                    return true;
                }

                IControlModeSession session = await server
                    .EnterControlModeAsync(sessionId, cancellationToken)
                    .ConfigureAwait(false);

                // A client with a size would drag the session's windows down to
                // it. This one exists to listen, so it opts out of the size
                // calculation entirely rather than relying on never having sent
                // one. The flag has been available since tmux 3.2.
                await session.SendAsync("refresh-client -f ignore-size", cancellationToken)
                    .ConfigureAwait(false);

                _session = session;
                _pump = PumpAsync(session);
                return true;
            }
            catch (LibTmuxException error)
            {
                if (hub._logger is not null)
                {
                    Log.ControlClientUnavailable(hub._logger, error, sessionId);
                }

                return false;
            }
            finally
            {
                _gate.Release();
            }
        }

        internal IAsyncDisposable Lease()
        {
            Interlocked.Increment(ref _leases);
            return new Release(this);
        }

        public async ValueTask DisposeAsync()
        {
            IControlModeSession? session = Interlocked.Exchange(ref _session, null);
            if (session is not null)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }

            if (_pump is Task pump)
            {
                await pump.ConfigureAwait(false);
            }

            _gate.Dispose();
        }

        private async Task PumpAsync(IControlModeSession session)
        {
            try
            {
                await foreach (TmuxEvent observed in session.Events.ConfigureAwait(false))
                {
                    switch (observed)
                    {
                        case TmuxOutputEvent output:
                            hub.OnPaneOutput(output.PaneId);
                            break;
                        case TmuxExitEvent exit when hub._logger is not null:
                            Log.ControlClientEnded(hub._logger, sessionId, exit.Reason);
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception error) when (error is LibTmuxException or OperationCanceledException)
            {
                // The client going away is how this ends. Waiters fall back to
                // their own timeout, which is why losing the stream degrades
                // cost rather than correctness.
            }
        }

        private async ValueTask ReleaseOneAsync()
        {
            if (Interlocked.Decrement(ref _leases) > 0)
            {
                return;
            }

            hub._watches.TryRemove(sessionId, out _);
            await DisposeAsync().ConfigureAwait(false);
        }

        private sealed class Release(SessionWatch watch) : IAsyncDisposable
        {
            private int _done;

            public ValueTask DisposeAsync() =>
                Interlocked.Exchange(ref _done, 1) == 0
                    ? watch.ReleaseOneAsync()
                    : ValueTask.CompletedTask;
        }
    }

    private sealed class NullLease : IAsyncDisposable
    {
        internal static NullLease Instance { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
