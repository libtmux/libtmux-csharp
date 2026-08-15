using System.Runtime.Versioning;
using LibTmux.Internal;

namespace LibTmux;

/// <summary>Starts, probes, and tears down a tmux server.</summary>
/// <remarks>
/// Liveness and teardown answer different questions, so they fail differently.
/// Probing whether a server is alive can honestly answer "no"; asking one to
/// kill a named session cannot honestly answer anything if the request never
/// lands.
/// </remarks>
public sealed partial class Server
{
    private const int SettleAttempts = 200;
    private static readonly TimeSpan SettleInterval = TimeSpan.FromMilliseconds(5);

    /// <summary>Starts the tmux server without creating a session.</summary>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <remarks>
    /// No handle comes back because there may be nothing to describe: a tmux
    /// server holding no sessions exits as soon as it starts. Materialize with
    /// <see cref="ConnectAsync(CancellationToken)" /> once a session exists.
    /// </remarks>
    [UnsupportedOSPlatform("windows")]
    public async Task StartServerAsync(CancellationToken cancellationToken = default)
    {
        TmuxCommandResult result = await Dispatch(["start-server"], cancellationToken)
            .ConfigureAwait(false);
        TmuxCommandFailure.ThrowIfFailed(result, "start-server");
    }

    /// <summary>Reports whether a tmux server is answering.</summary>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>True when the server responded.</returns>
    /// <remarks>A probe may answer no; it never reports a failure.</remarks>
    [UnsupportedOSPlatform("windows")]
    public async Task<bool> IsAliveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            TmuxCommandResult result = await Dispatch(["list-sessions"], cancellationToken)
                .ConfigureAwait(false);
            return result.ExitCode == 0;
        }
        catch (LibTmuxException)
        {
            return false;
        }
    }

    /// <summary>Throws unless a tmux server is answering.</summary>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    [UnsupportedOSPlatform("windows")]
    public async Task RaiseIfDeadAsync(CancellationToken cancellationToken = default)
    {
        TmuxCommandResult result = await Dispatch(["list-sessions"], cancellationToken)
            .ConfigureAwait(false);
        TmuxCommandFailure.ThrowIfFailed(result, "list-sessions");
    }

    /// <summary>Stops the tmux server.</summary>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <remarks>
    /// A server that is already gone is the requested outcome, so an absent
    /// daemon succeeds rather than failing.
    /// </remarks>
    [UnsupportedOSPlatform("windows")]
    public async Task KillAsync(CancellationToken cancellationToken = default)
    {
        TmuxCommandResult result = await Dispatch(["kill-server"], cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0 && !NamesMissingServer(result) && !NamesDyingServer(result))
        {
            TmuxCommandFailure.ThrowIfFailed(result, "kill-server");
        }
    }

    /// <summary>Stops one session.</summary>
    /// <param name="target">The session to stop.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    [UnsupportedOSPlatform("windows")]
    public async Task KillSessionAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        SessionName.Validate(target);
        TmuxCommandResult result = await Dispatch(
                ["kill-session", "-t", target],
                cancellationToken)
            .ConfigureAwait(false);
        TmuxCommandFailure.ThrowIfFailed(result, "kill-session");
    }

    /// <summary>Reports whether a session exists.</summary>
    /// <param name="target">The session to look for.</param>
    /// <param name="exact">Whether the name must match in full.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>True when tmux reports the session.</returns>
    [UnsupportedOSPlatform("windows")]
    public async Task<bool> HasSessionAsync(
        string target,
        bool exact = true,
        CancellationToken cancellationToken = default)
    {
        // The anchored form does not stop target splitting: tmux answers yes to
        // "=alpha:0" whenever session alpha has a window 0, which would make
        // this a window question about a name CreateSessionAsync would refuse.
        SessionName.Validate(target);
        // tmux treats -t as a prefix match, so an exact question needs the
        // anchored form or it would answer about a different session.
        TmuxCommandResult result = await Dispatch(
                ["has-session", "-t", exact ? $"={target}" : target],
                cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    /// <summary>Creates a session.</summary>
    /// <param name="request">The session to create.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The created session.</returns>
    /// <exception cref="TmuxSessionExistsException">The name is already taken.</exception>
    [UnsupportedOSPlatform("windows")]
    public async Task<Session> CreateSessionAsync(
        NewSessionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        NewSessionRequest options = request ?? new NewSessionRequest();
        if (options.Name is not null)
        {
            SessionName.Validate(options.Name);

            // tmux has no replace flag. Its nearest offer, new-session -A,
            // attaches to the existing session instead, which needs a terminal
            // and so fails outright from a library caller. Replacing therefore
            // means removing the old session first.
            if (options.ReplaceExisting
                && await HasSessionAsync(options.Name, true, cancellationToken)
                    .ConfigureAwait(false))
            {
                await KillSessionAsync(options.Name, cancellationToken).ConfigureAwait(false);
            }
        }

        TmuxCommandResult result = await Dispatch(
                [.. BuildNewSessionArguments(options)],
                cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0
            && options.Name is not null
            && result.StandardErrorLines.Any(static line =>
                line.Contains("duplicate session", StringComparison.Ordinal)))
        {
            throw new TmuxSessionExistsException(
                string.Join('\n', result.StandardErrorLines),
                options.Name);
        }

        TmuxCommandFailure.ThrowIfFailed(result, "new-session");
        string id = result.StandardOutputLines.Count > 0
            ? result.StandardOutputLines[0]
            : throw new InvalidDataException("tmux reported no new session identifier.");
        if (!SessionId.TryParse(id, out SessionId sessionId))
        {
            throw new InvalidDataException("tmux reported a malformed session identifier.");
        }

        // Materialize rather than resolve by identifier: a caller reads Name
        // straight off the created session, and that is snapshot state.
        // Deliberately not GetSessionsAsync: that accessor answers "what is
        // there" and reports empty on any failure, which would turn a
        // transient listing error into a misleading "session not reported".
        Server materialized = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = await RelationReader
            .ListAsync(materialized, "list-sessions", [], cancellationToken)
            .ConfigureAwait(false);
        IEnumerable<Session> sessions =
            rows.Select(row => RelationReader.ToSession(materialized, row));
        return sessions.FirstOrDefault(session => session.Id == sessionId)
            ?? throw new TmuxObjectNotFoundException(
                $"tmux did not report the created session '{sessionId}'.",
                sessionId.ToString());
    }

    /// <summary>Starts a server and takes ownership of it.</summary>
    /// <param name="options">Connection options.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>A scope that stops the server when disposed.</returns>
    /// <remarks>
    /// The scope holds an endpoint rather than a materialized server, because
    /// a tmux server with no sessions exits at once. Creating the first session
    /// through the endpoint both starts it for real and materializes it.
    /// </remarks>
    [UnsupportedOSPlatform("windows")]
    public static async Task<OwnedServerScope> CreateOwnedAsync(
        ServerConnectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Server endpoint = Open(options ?? ServerConnectionOptions.Default);
        await endpoint.StartServerAsync(cancellationToken).ConfigureAwait(false);
        await endpoint.WaitForSettledEndpointAsync(cancellationToken).ConfigureAwait(false);
        return new OwnedServerScope(endpoint);
    }

    /// <summary>Creates a session and takes ownership of it.</summary>
    /// <param name="request">The session to create.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>A scope that stops the session when disposed.</returns>
    [UnsupportedOSPlatform("windows")]
    public async Task<OwnedSessionScope> CreateOwnedSessionAsync(
        NewSessionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        Session created = await CreateSessionAsync(request, cancellationToken)
            .ConfigureAwait(false);
        return new OwnedSessionScope(created);
    }

    /// <summary>Attaches a client to a session on this server.</summary>
    /// <param name="request">Attachment options naming the session.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    [UnsupportedOSPlatform("windows")]
    public async Task AttachSessionAsync(
        AttachSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Target is null)
        {
            // A server-level attach has no session to fall back to, unlike the
            // session-level overload which attaches itself.
            throw new ArgumentException(
                "A server-level attach must name a target session.",
                nameof(request));
        }

        TmuxCommandResult result = await Dispatch(
                [.. Session.BuildAttachArguments(request, request.Target)],
                cancellationToken)
            .ConfigureAwait(false);
        TmuxCommandFailure.ThrowIfFailed(result, "attach-session");
    }

    internal static IEnumerable<string> BuildNewSessionArguments(NewSessionRequest options)
    {
        yield return "new-session";
        yield return "-P";
        yield return "-F";
        yield return "#{session_id}";
        if (!options.Attach)
        {
            yield return "-d";
        }

        if (options.DetachOthers)
        {
            yield return "-D";
        }

        if (options.NoSize)
        {
            yield return "-X";
        }

        foreach ((string flag, string? value) in new[]
        {
            ("-s", options.Name),
            ("-c", StartDirectory.Resolve(options.StartDirectory)),
            ("-n", options.WindowName),
            ("-x", options.Width),
            ("-y", options.Height),
            ("-f", options.ClientFlags),
        })
        {
            if (value is not null)
            {
                yield return flag;
                yield return value;
            }
        }

        if (options.Environment is not null)
        {
            foreach ((string key, string value) in options.Environment)
            {
                yield return "-e";
                yield return $"{key}={value}";
            }
        }

        if (options.Command is not null)
        {
            yield return options.Command;
        }
    }

    // tmux forks the daemon and returns before it notices it holds no sessions,
    // so for a moment the socket is neither serving nor gone and the next
    // command fails with "server exited unexpectedly" a few percent of the
    // time. Waiting for either settled answer closes that window.
    [UnsupportedOSPlatform("windows")]
    private async Task WaitForSettledEndpointAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < SettleAttempts; attempt++)
        {
            TmuxCommandResult result = await Dispatch(["list-sessions"], cancellationToken)
                .ConfigureAwait(false);
            if (result.ExitCode == 0 || NamesMissingServer(result))
            {
                return;
            }

            await Task.Delay(SettleInterval, cancellationToken).ConfigureAwait(false);
        }

        // An endpoint still in flux after the deadline is left for the caller's
        // next command to report, rather than failing here with less context.
    }

    // A socket that cannot be opened is not the same as a server that is
    // already gone: "error connecting to" also covers a permission error
    // against a live daemon, so on its own it must not read as absence.
    private static bool NamesMissingServer(TmuxCommandResult result)
    {
        string standardError = string.Join('\n', result.StandardErrorLines);
        return standardError.Contains("no server running", StringComparison.Ordinal)
            || (standardError.Contains("error connecting to", StringComparison.Ordinal)
                && standardError.Contains("No such file or directory", StringComparison.Ordinal));
    }

    // A server that dies mid-command reports neither presence nor absence.
    // Asking one to stop and finding it already stopping is the outcome that
    // was requested, so only kill treats this as success; waiting for a settled
    // endpoint must keep treating it as "not settled yet".
    private static bool NamesDyingServer(TmuxCommandResult result) =>
        result.StandardErrorLines.Any(static line =>
            line.Contains("server exited unexpectedly", StringComparison.Ordinal));

    [UnsupportedOSPlatform("windows")]
    private Task<TmuxCommandResult> Dispatch(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        Connection is null
            ? throw new InvalidOperationException("The server handle has no connection.")
            : Connection.ServerDispatcher.ExecuteAsync(arguments, cancellationToken);
}

/// <summary>Owns a server and stops it when disposed.</summary>
/// <remarks>
/// Ownership is explicit: a handle obtained any other way never tears its
/// server down, so a caller cannot accidentally kill a server it merely
/// connected to.
/// </remarks>
public sealed class OwnedServerScope : IAsyncDisposable
{
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);
    private int _disposed;

    internal OwnedServerScope(Server value) => Value = value;

    /// <summary>Gets the owned server.</summary>
    public Server Value { get; }

    /// <summary>Stops the owned server.</summary>
    /// <returns>A task that completes once the server is gone.</returns>
    /// <exception cref="LibTmuxException">The server could not be stopped.</exception>
    [UnsupportedOSPlatform("windows")]
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Teardown does not inherit the caller's token, because a canceled
        // caller still needs its server gone; it bounds itself instead so a
        // wedged socket cannot hang disposal forever.
        using CancellationTokenSource cleanup = new(CleanupTimeout);
        await Value.KillAsync(cleanup.Token).ConfigureAwait(false);
    }
}
