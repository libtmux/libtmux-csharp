using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace LibTmux;

/// <summary>Configures a tmux server connection without mutating process-wide state.</summary>
public sealed record ServerConnectionOptions
{
    /// <summary>Initializes connection options.</summary>
    public ServerConnectionOptions(
        string tmuxBinaryPath = "tmux",
        string? socketName = null,
        string? socketPath = null,
        Func<string>? socketNameFactory = null,
        string? configurationFile = null,
        TmuxColorMode colorMode = TmuxColorMode.Default,
        Func<Server, CancellationToken, ValueTask>? initializeAsync = null,
        IReadOnlyDictionary<string, string?>? childEnvironment = null,
        ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tmuxBinaryPath);
        if (socketName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(socketName);
        }

        if (socketPath is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);
        }

        if (configurationFile is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configurationFile);
        }

        if (!Enum.IsDefined(colorMode))
        {
            throw new ArgumentOutOfRangeException(nameof(colorMode));
        }

        Dictionary<string, string?>? childEnvironmentCopy = null;
        if (childEnvironment is not null)
        {
            childEnvironmentCopy = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach ((string key, string? value) in childEnvironment)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(key);
                if (key.Contains('\0') || key.Contains('='))
                {
                    throw new ArgumentException(
                        "Child environment variable names cannot contain NUL or '='.",
                        nameof(childEnvironment));
                }

                if (value is not null && value.Contains('\0'))
                {
                    throw new ArgumentException(
                        "Child environment variable values cannot contain NUL.",
                        nameof(childEnvironment));
                }

                childEnvironmentCopy.Add(key, value);
            }
        }

        TmuxBinaryPath = tmuxBinaryPath;
        SocketName = socketName;
        SocketPath = socketPath;
        SocketNameFactory = socketNameFactory;
        ConfigurationFile = configurationFile;
        ColorMode = colorMode;
        InitializeAsync = initializeAsync;
        ChildEnvironment = childEnvironmentCopy is null
            ? null
            : new ReadOnlyDictionary<string, string?>(childEnvironmentCopy);
        Logger = logger;
    }

    /// <summary>Gets conventional connection defaults.</summary>
    public static ServerConnectionOptions Default { get; } = new();

    /// <summary>Gets the tmux executable path.</summary>
    public string TmuxBinaryPath { get; }

    /// <summary>Gets the explicit socket name.</summary>
    public string? SocketName { get; }

    /// <summary>Gets the explicit socket path.</summary>
    public string? SocketPath { get; }

    /// <summary>Gets the deferred socket-name factory.</summary>
    public Func<string>? SocketNameFactory { get; }

    /// <summary>Gets the tmux configuration file.</summary>
    public string? ConfigurationFile { get; }

    /// <summary>Gets the requested tmux color mode.</summary>
    public TmuxColorMode ColorMode { get; }

    /// <summary>Gets the post-connect initializer.</summary>
    public Func<Server, CancellationToken, ValueTask>? InitializeAsync { get; }

    /// <summary>Gets the child-process environment overrides.</summary>
    public IReadOnlyDictionary<string, string?>? ChildEnvironment { get; }

    /// <summary>Gets the connection logger.</summary>
    public ILogger? Logger { get; }
}
