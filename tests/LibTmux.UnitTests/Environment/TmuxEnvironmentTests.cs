using System.Runtime.Versioning;

// A namespace segment named Environment would shadow System.Environment for
// every sibling file in this assembly, so these tests stay in the assembly
// root namespace even though the folder groups them.
using LibTmux.Internal;

namespace LibTmux.UnitTests;

[UnsupportedOSPlatform("windows")]
public sealed class TmuxEnvironmentTests
{
    private static Dictionary<string, string> Env(params (string Key, string Value)[] entries) =>
        entries.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value,
            StringComparer.Ordinal);

    [Fact]
    public void Reads_the_socket_pid_and_session_tmux_exports()
    {
        bool read = TmuxEnvironmentVariables.TryRead(
            Env(("TMUX", "/tmp/tmux-1000/default,4242,3")),
            out TmuxServerLocation? entry);

        Assert.True(read);
        Assert.Equal("/tmp/tmux-1000/default", entry!.SocketPath);
        Assert.Equal(4242, entry.ServerProcessId);
        Assert.Equal(SessionId.Parse("$3"), entry.SessionId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/tmp/socket,4242")]
    [InlineData("/tmp/socket,4242,3,extra")]
    [InlineData(",4242,3")]
    [InlineData("/tmp/socket,notapid,3")]
    [InlineData("/tmp/socket,4242,notasession")]
    public void Rejects_a_malformed_or_absent_server_variable(string value) =>
        Assert.False(TmuxEnvironmentVariables.TryRead(Env(("TMUX", value)), out _));

    [Fact]
    public void An_environment_without_tmux_names_no_server() =>
        Assert.False(TmuxEnvironmentVariables.TryRead(Env(("PATH", "/usr/bin")), out _));

    [Fact]
    public void Reads_the_pane_tmux_spawned_the_process_in()
    {
        Assert.True(TmuxEnvironmentVariables.TryReadPane(Env(("TMUX_PANE", "%7")), out PaneId pane));
        Assert.Equal(PaneId.Parse("%7"), pane);
        Assert.False(TmuxEnvironmentVariables.TryReadPane(Env(("TMUX_PANE", "seven")), out _));
        Assert.False(TmuxEnvironmentVariables.TryReadPane(Env(("PATH", "/usr/bin")), out _));
    }

    [Fact]
    public void Resolving_a_server_without_tmux_reports_the_missing_variable()
    {
        TmuxObjectNotFoundException error = Assert.Throws<TmuxObjectNotFoundException>(
            () => Server.FromEnvironment(Env(("PATH", "/usr/bin"))));

        Assert.Equal(TmuxEnvironmentVariables.ServerVariable, error.Target);
    }

    [Fact]
    public void Resolving_a_server_uses_only_the_socket_path()
    {
        // The pid and session id are frozen at pane spawn, so a handle built
        // from them would go stale; only the socket survives.
        Server server = Server.FromEnvironment(
            Env(("TMUX", "/tmp/tmux-1000/default,4242,3")));

        Assert.Equal("/tmp/tmux-1000/default", server.ConnectionOptions.SocketPath);
        Assert.False(server.IsMaterialized);
    }
}
