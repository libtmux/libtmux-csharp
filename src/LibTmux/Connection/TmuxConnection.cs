using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LibTmux.Internal;

internal sealed class TmuxConnection
{
    private const string GenerationFormat = "#{pid}:#{start_time}";
    private const string DefaultSocketRoot = "/tmp";
    private readonly Func<TmuxCommandRequest, CancellationToken, Task<TmuxCommandResult>> _execute;
    private readonly EndpointIdentity _endpointIdentity;
    private readonly Func<string> _markerFactory;

    [SuppressMessage(
        "Interoperability",
        "CA1416:Validate platform compatibility",
        Justification = "Stored delegates are invoked only by guarded process-backed members.")]
    internal TmuxConnection(ServerConnectionOptions options)
        : this(Resolve(options), execute: null, markerFactory: null)
    {
    }

    internal TmuxConnection(
        ServerConnectionOptions options,
        Func<TmuxCommandRequest, CancellationToken, Task<TmuxCommandResult>> execute,
        Func<string>? markerFactory = null)
        : this(Resolve(options), execute, markerFactory)
    {
    }

    [SuppressMessage(
        "Interoperability",
        "CA1416:Validate platform compatibility",
        Justification = "Stored delegates are invoked only by guarded process-backed members.")]
    private TmuxConnection(
        ResolvedConnection resolved,
        Func<TmuxCommandRequest, CancellationToken, Task<TmuxCommandResult>>? execute,
        Func<string>? markerFactory)
    {
        Options = resolved.Options;
        PrefixArguments = resolved.PrefixArguments;
        _endpointIdentity = resolved.EndpointIdentity;

        if (execute is null)
        {
            var transport = new TmuxProcessTransport(
                Options.TmuxBinaryPath,
                PrefixArguments,
                launcher: startInfo =>
                {
                    ApplyChildEnvironment(startInfo, resolved.ChildEnvironment);
                    return Process.Start(startInfo)
                        ?? throw new InvalidOperationException("The tmux client process did not start.");
                });
            _execute = (request, cancellationToken) =>
            {
                PlatformGuard.ThrowIfWindows();
                return transport.ExecuteAsync(request, cancellationToken);
            };
        }
        else
        {
            _execute = execute;
        }

        CommandContext = Options.Logger is ILogger logger
            ? new TmuxCommandContext(logger, Options.SocketName ?? Options.SocketPath)
            : null;
        ServerDispatcher = new TmuxCommandDispatcher(
            (arguments, cancellationToken) =>
            {
                PlatformGuard.ThrowIfWindows();
                return ExecuteSingleAsync(arguments, cancellationToken);
            },
            CommandContext,
            (commands, cancellationToken) =>
            {
                PlatformGuard.ThrowIfWindows();
                return _execute(TmuxCommandRequest.Group([.. commands]), cancellationToken);
            });
        _markerFactory = markerFactory ?? (static () => $"libtmux_stale_{Guid.NewGuid():N}");
    }

    internal ServerConnectionOptions Options { get; }

    internal IReadOnlyList<string> PrefixArguments { get; }

    internal TmuxCommandDispatcher ServerDispatcher { get; }

    internal TmuxCommandContext? CommandContext { get; }

    internal bool HasSameEndpoint(TmuxConnection other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return _endpointIdentity == other._endpointIdentity;
    }

    internal int GetEndpointHashCode() => _endpointIdentity.GetHashCode();

    [UnsupportedOSPlatform("windows")]
    internal async Task<(ServerGeneration Generation, string RawVersion)> DiscoverAsync(
        CancellationToken cancellationToken)
    {
        PlatformGuard.ThrowIfWindows();
        TmuxCommandResult generationResult = await ExecuteSingleAsync(
            ["display-message", "-p", GenerationFormat],
            cancellationToken).ConfigureAwait(false);
        EnsureSuccessful(generationResult, "server generation discovery");
        if (generationResult.StandardOutputLines.Count != 1)
        {
            throw new InvalidDataException("tmux did not report exactly one server generation.");
        }

        ServerGeneration generation = ParseGeneration(generationResult.StandardOutputLines[0]);
        TmuxCommandResult versionResult = await ExecuteSingleAsync(
            ["-V"],
            cancellationToken).ConfigureAwait(false);
        EnsureSuccessful(versionResult, "tmux version discovery");
        if (versionResult.StandardOutputLines.Count != 1)
        {
            throw new InvalidDataException("tmux did not report exactly one version line.");
        }

        return (generation, versionResult.StandardOutputLines[0]);
    }

    [UnsupportedOSPlatform("windows")]
    internal async Task<(ServerGeneration Generation, SessionId Id)?> FindSessionAsync(
        SessionId id,
        CancellationToken cancellationToken)
    {
        PlatformGuard.ThrowIfWindows();
        TmuxCommandResult result = await ExecuteSingleAsync(
            ["list-sessions", "-F", $"{GenerationFormat}\t#{{session_id}}"],
            cancellationToken).ConfigureAwait(false);
        EnsureSuccessful(result, "session lookup");
        foreach (string line in result.StandardOutputLines)
        {
            (ServerGeneration Generation, string Text) fields = ParseIdentityRow(line, "session");
            if (!SessionId.TryParse(fields.Text, out SessionId candidate))
            {
                throw new InvalidDataException("tmux reported a malformed session identifier.");
            }

            if (candidate == id)
            {
                return (fields.Generation, candidate);
            }
        }

        return null;
    }

    [UnsupportedOSPlatform("windows")]
    internal async Task<(ServerGeneration Generation, WindowId Id)?> FindWindowAsync(
        WindowId id,
        CancellationToken cancellationToken)
    {
        PlatformGuard.ThrowIfWindows();
        TmuxCommandResult result = await ExecuteSingleAsync(
            ["list-windows", "-a", "-F", $"{GenerationFormat}\t#{{window_id}}"],
            cancellationToken).ConfigureAwait(false);
        EnsureSuccessful(result, "window lookup");
        var seen = new HashSet<(ServerGeneration Generation, WindowId Id)>();
        foreach (string line in result.StandardOutputLines)
        {
            (ServerGeneration Generation, string Text) fields = ParseIdentityRow(line, "window");
            if (!WindowId.TryParse(fields.Text, out WindowId candidate))
            {
                throw new InvalidDataException("tmux reported a malformed window identifier.");
            }

            var identity = (fields.Generation, candidate);
            if (seen.Add(identity) && candidate == id)
            {
                return identity;
            }
        }

        return null;
    }

    [UnsupportedOSPlatform("windows")]
    internal async Task<(ServerGeneration Generation, PaneId Id)?> FindPaneAsync(
        PaneId id,
        CancellationToken cancellationToken)
    {
        PlatformGuard.ThrowIfWindows();
        TmuxCommandResult result = await ExecuteSingleAsync(
            ["list-panes", "-a", "-F", $"{GenerationFormat}\t#{{pane_id}}"],
            cancellationToken).ConfigureAwait(false);
        EnsureSuccessful(result, "pane lookup");
        var seen = new HashSet<(ServerGeneration Generation, PaneId Id)>();
        foreach (string line in result.StandardOutputLines)
        {
            (ServerGeneration Generation, string Text) fields = ParseIdentityRow(line, "pane");
            if (!PaneId.TryParse(fields.Text, out PaneId candidate))
            {
                throw new InvalidDataException("tmux reported a malformed pane identifier.");
            }

            var identity = (fields.Generation, candidate);
            if (seen.Add(identity) && candidate == id)
            {
                return identity;
            }
        }

        return null;
    }

    [UnsupportedOSPlatform("windows")]
    internal TmuxCommandDispatcher CreateEntityDispatcher(ServerGeneration generation)
    {
        ValidateLiveGeneration(generation);
        return new TmuxCommandDispatcher(
            (arguments, cancellationToken) => ExecuteGuardedAsync(
                generation,
                arguments,
                cancellationToken),
            CommandContext);
    }

    internal static ServerGeneration ParseGeneration(string text)
    {
        string[] fields = text.Split(':');
        if (fields.Length != 2
            || !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out int processId)
            || !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out long startTime))
        {
            throw new InvalidDataException("tmux reported a malformed server generation.");
        }

        try
        {
            return new ServerGeneration(processId, startTime);
        }
        catch (ArgumentOutOfRangeException error)
        {
            throw new InvalidDataException("tmux reported a nonpositive server generation.", error);
        }
    }

    internal static void ApplyChildEnvironment(
        ProcessStartInfo startInfo,
        IReadOnlyDictionary<string, string?>? childEnvironment)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        startInfo.Environment.Remove("TMUX");
        if (childEnvironment is null)
        {
            return;
        }

        foreach ((string key, string? value) in childEnvironment)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            if (value is null)
            {
                startInfo.Environment.Remove(key);
            }
            else
            {
                startInfo.Environment[key] = value;
            }
        }
    }

    private static string[] BuildPrefixArguments(
        ServerConnectionOptions options,
        string? socketPath,
        string? socketName)
    {
        var arguments = new List<string>();
        switch (options.ColorMode)
        {
            case TmuxColorMode.Default:
                break;
            case TmuxColorMode.Colors256:
                arguments.Add("-2");
                break;
            case TmuxColorMode.TrueColor:
                arguments.Add("-T");
                arguments.Add("RGB");
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.ColorMode,
                    "The tmux color mode is not defined.");
        }

        if (options.ConfigurationFile is not null)
        {
            arguments.Add("-f");
            arguments.Add(options.ConfigurationFile);
        }

        if (socketPath is not null)
        {
            arguments.Add("-S");
            arguments.Add(socketPath);
        }
        else if (socketName is not null)
        {
            arguments.Add("-L");
            arguments.Add(socketName);
        }

        return [.. arguments];
    }

    private static ResolvedConnection Resolve(ServerConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        string? socketPath = options.SocketPath is null
            ? null
            : Path.GetFullPath(options.SocketPath);
        string? socketName = null;
        IReadOnlyDictionary<string, string?>? childEnvironment = options.ChildEnvironment;
        EndpointIdentity endpointIdentity;
        if (socketPath is null)
        {
            socketName = options.SocketName;
            if (socketName is null && options.SocketNameFactory is not null)
            {
                socketName = options.SocketNameFactory();
                if (string.IsNullOrWhiteSpace(socketName))
                {
                    throw new InvalidOperationException(
                        "The selected socket-name factory returned no usable name.");
                }
            }

            socketName ??= "default";
            ResolvedSocketRoot socketRoot = ResolveSocketRoot(options.ChildEnvironment);
            childEnvironment = FreezeSocketRoot(
                options.ChildEnvironment,
                socketRoot.EnvironmentValue);
            endpointIdentity = EndpointIdentity.ForName(socketRoot.Identity, socketName);
        }
        else
        {
            endpointIdentity = EndpointIdentity.ForPath(socketPath);
        }

        return new ResolvedConnection(
            options,
            BuildPrefixArguments(options, socketPath, socketName),
            endpointIdentity,
            childEnvironment);
    }

    private static ResolvedSocketRoot ResolveSocketRoot(
        IReadOnlyDictionary<string, string?>? childEnvironment)
    {
        string? configuredRoot;
        if (childEnvironment is null
            || !childEnvironment.TryGetValue("TMUX_TMPDIR", out configuredRoot))
        {
            configuredRoot = Environment.GetEnvironmentVariable("TMUX_TMPDIR");
        }

        if (string.IsNullOrEmpty(configuredRoot))
        {
            return new ResolvedSocketRoot(
                NormalizeSocketRoot(DefaultSocketRoot),
                EnvironmentValue: null);
        }

        string normalizedRoot = NormalizeSocketRoot(configuredRoot);
        return new ResolvedSocketRoot(normalizedRoot, normalizedRoot);
    }

    private static ReadOnlyDictionary<string, string?> FreezeSocketRoot(
        IReadOnlyDictionary<string, string?>? childEnvironment,
        string? socketRoot)
    {
        var frozen = childEnvironment is null
            ? new Dictionary<string, string?>(StringComparer.Ordinal)
            : new Dictionary<string, string?>(childEnvironment, StringComparer.Ordinal);
        frozen["TMUX_TMPDIR"] = socketRoot;
        return new ReadOnlyDictionary<string, string?>(frozen);
    }

    private static string NormalizeSocketRoot(string socketRoot) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(socketRoot));

    private static (ServerGeneration Generation, string Text) ParseIdentityRow(
        string line,
        string kind)
    {
        string[] fields = line.Split('\t');
        if (fields.Length != 2)
        {
            throw new InvalidDataException($"tmux reported a malformed {kind} identity row.");
        }

        return (ParseGeneration(fields[0]), fields[1]);
    }

    [UnsupportedOSPlatform("windows")]
    private async Task<TmuxCommandResult> ExecuteGuardedAsync(
        ServerGeneration expected,
        IReadOnlyList<string> logicalArguments,
        CancellationToken cancellationToken)
    {
        PlatformGuard.ThrowIfWindows();
        ValidateLiveGeneration(expected);
        TmuxCommandDispatcher.ValidateArguments(logicalArguments);
        string marker = _markerFactory();
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        string generationText = $"{expected.ProcessId.ToString(CultureInfo.InvariantCulture)}:{expected.StartTime.ToString(CultureInfo.InvariantCulture)}";
        TmuxCommandRequest request = TmuxCommandRequest.Group(
            ["display-message", "-p", GenerationFormat],
            ["if-shell", "-F", $"#{{==:{GenerationFormat},{generationText}}}", string.Empty, marker],
            logicalArguments);

        TmuxCommandResult grouped;
        try
        {
            grouped = await _execute(request, cancellationToken).ConfigureAwait(false);
        }
        catch (TmuxTransportException error)
        {
            throw new TmuxTransportException(
                error.Message,
                logicalArguments,
                error.InnerException);
        }

        if (!TryStripGenerationPrefix(
                grouped.StandardOutput.Span,
                out ServerGeneration actual,
                out byte[] remainingOutput))
        {
            bool exactMarkerFailure = grouped.ExitCode == 1
                && IsExactMarkerFailure(grouped.StandardError.Span, marker);
            if (grouped.ExitCode != 0 && !exactMarkerFailure)
            {
                return RemapResult(grouped, logicalArguments, grouped.StandardOutput);
            }

            throw new InvalidDataException(
                "tmux did not return a valid leading generation line.");
        }

        if (grouped.ExitCode == 1 && IsExactMarkerFailure(grouped.StandardError.Span, marker))
        {
            throw new StaleServerGenerationException(
                $"The tmux server generation changed from {generationText} to {actual.ProcessId.ToString(CultureInfo.InvariantCulture)}:{actual.StartTime.ToString(CultureInfo.InvariantCulture)}.",
                expected,
                actual);
        }

        return RemapResult(grouped, logicalArguments, remainingOutput);
    }

    private static bool TryStripGenerationPrefix(
        ReadOnlySpan<byte> standardOutput,
        out ServerGeneration generation,
        out byte[] remainingOutput)
    {
        int lineEnd = standardOutput.IndexOf((byte)'\n');
        if (lineEnd < 0)
        {
            generation = default;
            remainingOutput = [];
            return false;
        }

        ReadOnlySpan<byte> generationBytes = standardOutput[..lineEnd];
        if (!generationBytes.IsEmpty && generationBytes[^1] == '\r')
        {
            generationBytes = generationBytes[..^1];
        }

        try
        {
            generation = ParseGeneration(Encoding.UTF8.GetString(generationBytes));
        }
        catch (InvalidDataException)
        {
            generation = default;
            remainingOutput = [];
            return false;
        }

        remainingOutput = standardOutput[(lineEnd + 1)..].ToArray();
        return true;
    }

    private static TmuxCommandResult RemapResult(
        TmuxCommandResult grouped,
        IReadOnlyList<string> logicalArguments,
        ReadOnlyMemory<byte> standardOutput) =>
        new(
            logicalArguments,
            grouped.ExitCode,
            standardOutput,
            grouped.StandardError,
            Utf8BackslashDecoder.ProjectOutputLines(standardOutput.Span),
            Utf8BackslashDecoder.ProjectErrorLines(grouped.StandardError.Span));

    private static bool IsExactMarkerFailure(ReadOnlySpan<byte> standardError, string marker)
    {
        byte[] expected = Encoding.UTF8.GetBytes($"unknown command: {marker}\n");
        return standardError.SequenceEqual(expected);
    }

    private static void ValidateLiveGeneration(ServerGeneration generation)
    {
        if (generation.ProcessId <= 0 || generation.StartTime <= 0)
        {
            throw new ArgumentException("A live handle requires a positive server generation.", nameof(generation));
        }
    }

    private sealed record ResolvedConnection(
        ServerConnectionOptions Options,
        string[] PrefixArguments,
        EndpointIdentity EndpointIdentity,
        IReadOnlyDictionary<string, string?>? ChildEnvironment);

    private readonly record struct ResolvedSocketRoot(
        string Identity,
        string? EnvironmentValue);

    private readonly record struct EndpointIdentity(
        EndpointKind Kind,
        string Primary,
        string? Secondary)
    {
        internal static EndpointIdentity ForPath(string socketPath) =>
            new(EndpointKind.Path, socketPath, Secondary: null);

        internal static EndpointIdentity ForName(string socketRoot, string socketName) =>
            new(EndpointKind.Name, socketRoot, socketName);
    }

    private enum EndpointKind
    {
        Path,
        Name,
    }

    [UnsupportedOSPlatform("windows")]
    private Task<TmuxCommandResult> ExecuteSingleAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        PlatformGuard.ThrowIfWindows();
        return _execute(TmuxCommandRequest.Single(arguments), cancellationToken);
    }

    private static void EnsureSuccessful(TmuxCommandResult result, string operation)
    {
        if (result.ExitCode != 0 || result.StandardErrorLines.Count > 0)
        {
            throw new TmuxCommandException($"{operation} failed.", result);
        }
    }
}
