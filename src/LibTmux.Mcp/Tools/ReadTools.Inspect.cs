using System.ComponentModel;
using System.Runtime.Versioning;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <content>Reading tmux's own settings, and finding servers to read at all.</content>
[UnsupportedOSPlatform("windows")]
public sealed partial class ReadTools
{
    /// <summary>Finds the tmux servers running for this user.</summary>
    /// <param name="cancellationToken">Cancels the probes.</param>
    /// <returns>The sockets, and whether each answers.</returns>
    /// <remarks>
    /// A socket file outlives the server that made it, so the listing is
    /// candidates until each is asked. Answering with dead sockets would send
    /// a caller to a server that is not there.
    /// </remarks>
    [McpServerTool(Name = "tmux_list_servers", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Find the tmux servers running for this user, by socket. Use when a session "
        + "you expect is missing: it is usually on a different socket. Every other "
        + "tool takes a socketName to reach one of these.")]
    public async Task<IReadOnlyList<DiscoveredServer>> ListServersAsync(
        CancellationToken cancellationToken = default)
    {
        List<DiscoveredServer> found = [];
        foreach (string path in SocketCandidates())
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = Path.GetFileName(path);
            bool alive;
            int sessions = 0;
            try
            {
                Server probe = await _connection.GetAsync(name, cancellationToken)
                    .ConfigureAwait(false);
                alive = await probe.IsAliveAsync(cancellationToken).ConfigureAwait(false);
                if (alive)
                {
                    sessions = (await probe.GetSessionsAsync(cancellationToken).ConfigureAwait(false))
                        .Count;
                }
            }
            catch (Exception error) when (error is McpException or LibTmuxException)
            {
                alive = false;
            }

            found.Add(new DiscoveredServer(
                SocketName: name,
                SocketPath: path,
                Alive: alive,
                SessionCount: sessions,
                IsDefault: string.Equals(name, _connection.DefaultSocketName ?? "default", StringComparison.Ordinal)));
        }

        return found;
    }

    /// <summary>Expands a tmux format against the server.</summary>
    /// <param name="format">The format string, such as <c>#{session_name}</c>.</param>
    /// <param name="paneId">The pane to expand it against, or null for the active one.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What tmux expanded it to.</returns>
    /// <remarks>
    /// The escape hatch. tmux exposes far more through formats than any fixed
    /// set of tools can, and a caller who knows the field name should not need
    /// a new tool to read it.
    /// </remarks>
    [McpServerTool(Name = "tmux_display_message", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Expand a tmux FORMAT string, such as '#{pane_current_command}' or "
        + "'#{window_layout}', and return the text. Use to read any tmux field the "
        + "other tools do not expose. See the FORMATS section of the tmux manual for "
        + "field names.")]
    public async Task<string?> DisplayMessageAsync(
        [Description("A tmux format string, such as #{session_name}.")] string format,
        [Description("The pane to expand it against, such as %1. Omit for the active pane.")]
        string? paneId = null,
        [Description("The tmux socket to use. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        Pane pane = await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
            .ConfigureAwait(false);
        return await TmuxTargets.DisplayAsync(pane, format, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads tmux options.</summary>
    /// <param name="name">One option to read, or null for all of them.</param>
    /// <param name="scope">Which level to read.</param>
    /// <param name="paneId">The pane whose scope to read, or null for the active one.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The options.</returns>
    [McpServerTool(Name = "tmux_show_options", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Read tmux options at the server, session, window or pane level. Omit the name "
        + "to list them all. Reading history-limit before a long tail tells you how "
        + "much output the pane can hold before it starts dropping lines.")]
    public async Task<IReadOnlyList<OptionEntry>> ShowOptionsAsync(
        [Description("One option name, such as history-limit. Omit to list every option.")]
        string? name = null,
        [Description("Which level to read: Server, Session, Window or Pane.")]
        OptionScope scope = OptionScope.Pane,
        [Description("The pane whose scope to read, such as %1. Omit for the active pane.")]
        string? paneId = null,
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        TmuxOptions options = await OptionsForAsync(server, scope, paneId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<TmuxOption> read = string.IsNullOrWhiteSpace(name)
            ? await options.GetAllAsync(new GetOptionsRequest(quiet: true), cancellationToken)
                .ConfigureAwait(false)
            : await options.GetAsync(new GetOptionRequest(name, quiet: true), cancellationToken)
                .ConfigureAwait(false);

        return [.. read.Select(each => new OptionEntry(each.Name, each.Value.Raw, scope))];
    }

    /// <summary>Reads tmux's environment.</summary>
    /// <param name="name">One variable to read, or null for all of them.</param>
    /// <param name="session">The session whose environment to read, or null for the server's.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The variables.</returns>
    [McpServerTool(Name = "tmux_show_environment", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Read the environment tmux gives to new panes, at the server or session level. "
        + "This is what a NEW pane will inherit, not what an already-running shell has.")]
    public async Task<IReadOnlyList<EnvironmentEntry>> ShowEnvironmentAsync(
        [Description("One variable name. Omit to list them all.")] string? name = null,
        [Description("A session id such as $0, or its name. Omit for the server's environment.")]
        string? session = null,
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        TmuxEnvironment environment = string.IsNullOrWhiteSpace(session)
            ? server.Environment
            : (await TmuxTargets.SessionAsync(server, session, cancellationToken)
                .ConfigureAwait(false)).Environment;

        if (!string.IsNullOrWhiteSpace(name))
        {
            TmuxEnvironmentEntry? one = await environment.GetAsync(name, cancellationToken)
                .ConfigureAwait(false);
            return one is null ? [] : [new EnvironmentEntry(one.Name, one.Value, one.IsRemoved)];
        }

        IReadOnlyList<TmuxEnvironmentEntry> all = await environment.GetAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. all.Select(each => new EnvironmentEntry(each.Name, each.Value, each.IsRemoved))];
    }

    /// <summary>Reads the hooks tmux will run.</summary>
    /// <param name="scope">Which level to read.</param>
    /// <param name="paneId">The pane whose scope to read, or null for the active one.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The hooks.</returns>
    /// <remarks>
    /// Reading only. A hook outlives the process that set it, so one written
    /// here would keep firing long after this conversation ended, with nobody
    /// left who knows why. Hooks meant to last belong in a tmux config file.
    /// </remarks>
    [McpServerTool(Name = "tmux_show_hooks", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Read the hooks tmux will run on its own events. Read-only on purpose: a hook "
        + "written here would outlive this conversation and keep firing with nobody "
        + "left who knows why. Put hooks you want to keep in your tmux config file.")]
    public async Task<IReadOnlyList<HookEntry>> ShowHooksAsync(
        [Description("Which level to read: Server, Session, Window or Pane.")]
        OptionScope scope = OptionScope.Session,
        [Description("The pane whose scope to read, such as %1. Omit for the active pane.")]
        string? paneId = null,
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        TmuxHooks hooks = await HooksForAsync(server, scope, paneId, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<TmuxHook> all = await hooks
            .GetAllAsync(new ListHooksRequest(), cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. all.SelectMany(hook => hook.Values.Select(entry =>
                new HookEntry(hook.Name, entry.Index, entry.Command, scope))),
        ];
    }

    /// <summary>Lists the paste buffers.</summary>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The buffers, by name and size.</returns>
    /// <remarks>
    /// Names and sizes only. tmux's buffer stack collects whatever a user
    /// copied, which is a plausible place for a password to be sitting, so
    /// reading contents is a separate and deliberate call.
    /// </remarks>
    [McpServerTool(Name = "tmux_list_buffers", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "List tmux paste buffers by name and size, without their contents. Buffers "
        + "hold whatever the user copied, so read one only when you actually need it.")]
    public async Task<IReadOnlyList<BufferEntry>> ListBuffersAsync(
        [Description("The tmux socket to read. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TmuxBuffer> buffers = await server.GetBuffersAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. buffers.Select(each => new BufferEntry(each.Name, each.Size))];
    }

    /// <summary>Lists the socket files this user could have made.</summary>
    /// <remarks>
    /// tmux puts its sockets in <c>$TMUX_TMPDIR/tmux-$UID</c>, defaulting to
    /// <c>/tmp</c>. The user id is not read here: every <c>tmux-*</c> directory
    /// is tried and the ones belonging to other users refuse to be listed,
    /// which answers the same question without a platform call.
    /// </remarks>
    private static List<string> SocketCandidates()
    {
        string root = System.Environment.GetEnvironmentVariable("TMUX_TMPDIR") is string named
            && !string.IsNullOrWhiteSpace(named)
                ? named
                : "/tmp";

        List<string> sockets = [];
        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(root, "tmux-*");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return sockets;
        }

        foreach (string directory in directories)
        {
            try
            {
                sockets.AddRange(Directory.EnumerateFiles(directory));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Another user's socket directory. Not ours to list.
            }
        }

        sockets.Sort(StringComparer.Ordinal);
        return sockets;
    }

    private static async Task<TmuxOptions> OptionsForAsync(
        Server server,
        OptionScope scope,
        string? paneId,
        CancellationToken cancellationToken) => scope switch
        {
            OptionScope.Server => server.Options,
            OptionScope.Session => (await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
                .ConfigureAwait(false)).Session.Options,
            OptionScope.Window => (await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
                .ConfigureAwait(false)).Window.Options,
            _ => (await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
                .ConfigureAwait(false)).Options,
        };

    private static async Task<TmuxHooks> HooksForAsync(
        Server server,
        OptionScope scope,
        string? paneId,
        CancellationToken cancellationToken) => scope switch
        {
            OptionScope.Server => server.Hooks,
            OptionScope.Window => (await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
                .ConfigureAwait(false)).Window.Hooks,
            OptionScope.Pane => (await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
                .ConfigureAwait(false)).Hooks,
            _ => (await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
                .ConfigureAwait(false)).Session.Hooks,
        };
}

/// <summary>A tmux socket found on this machine.</summary>
/// <param name="SocketName">The name to pass as <c>socketName</c>.</param>
/// <param name="SocketPath">Where the socket file is.</param>
/// <param name="Alive">Whether a server actually answered on it.</param>
/// <param name="SessionCount">How many sessions it holds.</param>
/// <param name="IsDefault">Whether this is the socket tools use when none is named.</param>
public sealed record DiscoveredServer(
    string SocketName,
    string SocketPath,
    bool Alive,
    int SessionCount,
    bool IsDefault);

/// <summary>One tmux option.</summary>
/// <param name="Name">The option name.</param>
/// <param name="Value">Its value as tmux reports it.</param>
/// <param name="Scope">The level it was read at.</param>
public sealed record OptionEntry(string Name, string? Value, OptionScope Scope);

/// <summary>One variable in tmux's environment.</summary>
/// <param name="Name">The variable name.</param>
/// <param name="Value">Its value, or null when it is marked removed.</param>
/// <param name="IsRemoved">Whether tmux will unset it for a new pane.</param>
public sealed record EnvironmentEntry(string Name, string? Value, bool IsRemoved);

/// <summary>One command tmux will run on an event.</summary>
/// <param name="Name">The hook name, such as <c>pane-exited</c>.</param>
/// <param name="Index">Its position, when the hook holds several commands.</param>
/// <param name="Command">The tmux command.</param>
/// <param name="Scope">The level it was read at.</param>
public sealed record HookEntry(string Name, int Index, string Command, OptionScope Scope);

/// <summary>One paste buffer, without its contents.</summary>
/// <param name="Name">The buffer name.</param>
/// <param name="Size">How many bytes it holds.</param>
public sealed record BufferEntry(string Name, long Size);
