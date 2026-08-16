using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace LibTmux.Mcp;

/// <summary>Tells subscribed clients when the tmux hierarchy changed.</summary>
/// <remarks>
/// <para>
/// tmux reports a window appearing, a session changing or a layout moving to a
/// client in control mode, without being asked. Forwarding those as resource
/// updates means a client's view of the hierarchy invalidates itself, instead
/// of being re-listed on a timer in case something moved.
/// </para>
/// <para>
/// One control client for the whole server, started only once somebody
/// subscribes, and stopped when the last subscriber goes. It attaches with
/// <c>no-output</c> as well as <c>ignore-size</c>: this one wants to hear about
/// the hierarchy and not about every byte a pane prints, and tmux will keep
/// the pane traffic out of the stream if asked.
/// </para>
/// </remarks>
[UnsupportedOSPlatform("windows")]
public sealed class HierarchyWatcher : IAsyncDisposable
{
    /// <summary>The notifications that mean the hierarchy is not what it was.</summary>
    /// <remarks>
    /// Named rather than "anything tmux says": a bell or an activity flag is
    /// not a change to what exists, and waking every subscriber for one would
    /// make the subscription cost more than the polling it replaces.
    /// </remarks>
    private static readonly HashSet<string> Structural = new(StringComparer.Ordinal)
    {
        "session-changed",
        "session-renamed",
        "sessions-changed",
        "window-add",
        "window-close",
        "window-renamed",
        "window-pane-changed",
        "layout-change",
        "unlinked-window-add",
        "unlinked-window-close",
        "pane-mode-changed",
        "client-detached",
    };

    private readonly ConcurrentDictionary<string, byte> _subscribed = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger? _logger;
    private IControlModeSession? _session;
    private Task? _pump;
    private Func<IReadOnlyList<string>, Task>? _announce;

    /// <summary>Initializes the watcher.</summary>
    /// <param name="logger">Records why a control client could not start.</param>
    public HierarchyWatcher(ILogger? logger = null) => _logger = logger;

    /// <summary>Gets the resource URIs this watcher will notify about.</summary>
    /// <remarks>
    /// The ones whose content a structural change can alter. A pane's text
    /// changes constantly and is not a structural change, so it is not here:
    /// a subscription that fired on every keystroke would be a firehose.
    /// </remarks>
    public static IReadOnlyList<string> Watchable { get; } =
    [
        "tmux://hierarchy",
        "tmux://sessions",
        "tmux://servers",
    ];

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _subscribed.Clear();
        await StopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    /// <summary>Starts reporting changes to one resource.</summary>
    /// <param name="uri">The resource the client subscribed to.</param>
    /// <param name="announce">
    /// Told which resources changed. Passed in rather than known, because how a
    /// change reaches a client is the protocol's business and moves with it —
    /// the revision that replaced <c>resources/subscribe</c> with
    /// <c>subscriptions/listen</c> changed the delivery and not this.
    /// </param>
    /// <param name="tmux">The tmux server to watch.</param>
    /// <param name="cancellationToken">Cancels starting the control client.</param>
    public async Task SubscribeAsync(
        string uri,
        Func<IReadOnlyList<string>, Task> announce,
        Server tmux,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(announce);
        ArgumentNullException.ThrowIfNull(tmux);

        _announce = announce;
        _subscribed[uri] = 0;
        await EnsureStartedAsync(tmux, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stops reporting changes to one resource.</summary>
    /// <param name="uri">The resource the client unsubscribed from.</param>
    public async Task UnsubscribeAsync(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        _subscribed.TryRemove(uri, out _);
        if (_subscribed.IsEmpty)
        {
            await StopAsync().ConfigureAwait(false);
        }
    }

    private async Task EnsureStartedAsync(Server tmux, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_session is not null)
            {
                return;
            }

            IControlModeSession session = await tmux
                .EnterControlModeAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await session
                .SendAsync("refresh-client -f ignore-size,no-output", cancellationToken)
                .ConfigureAwait(false);

            _session = session;
            _pump = PumpAsync(session);
        }
        catch (LibTmuxException error)
        {
            // Without a control client there are no notifications, and a client
            // that subscribed simply never hears one. Reading still works, so
            // this costs freshness rather than function.
            if (_logger is not null)
            {
                Log.ControlClientUnavailable(_logger, error, "hierarchy");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopAsync()
    {
        IControlModeSession? session = Interlocked.Exchange(ref _session, null);
        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }

        if (_pump is Task pump)
        {
            await pump.ConfigureAwait(false);
            _pump = null;
        }
    }

    private async Task PumpAsync(IControlModeSession session)
    {
        try
        {
            await foreach (TmuxEvent observed in session.Events.ConfigureAwait(false))
            {
                if (observed is TmuxNotificationEvent notification
                    && Structural.Contains(notification.Name))
                {
                    await NotifyAsync().ConfigureAwait(false);
                }
            }
        }
        catch (Exception error) when (error is LibTmuxException or OperationCanceledException)
        {
            // The client going away is how this ends.
        }
    }

    private async Task NotifyAsync()
    {
        if (_announce is not Func<IReadOnlyList<string>, Task> announce)
        {
            return;
        }

        string[] changed = [.. _subscribed.Keys];
        if (changed.Length == 0)
        {
            return;
        }

        try
        {
            await announce(changed).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException
            or InvalidOperationException)
        {
            // The client hung up. Nothing here is worth failing over, and the
            // subscription dies with the session anyway.
        }
    }

    /// <summary>Answers whether a tmux notification changes what exists.</summary>
    /// <param name="name">The notification name, without its leading percent.</param>
    /// <returns><see langword="true" /> when subscribers should be told.</returns>
    internal static bool IsStructural(string name) => Structural.Contains(name);
}
