using System.Runtime.CompilerServices;

namespace LibTmux.Engineering;

/// <summary>Keeps this repository's tmux out of the socket root other ports share.</summary>
/// <remarks>
/// Several libtmux ports run real tmux on one machine, and every one of them
/// can reach a socket in the default root. One port's cleanup sweep then kills
/// another port's servers mid-run, and the failure surfaces in whichever suite
/// noticed rather than the one that caused it, which is what makes it expensive
/// to diagnose.
///
/// This runs before anything else in the assembly, because a root claimed after
/// the first server has started does not move that server.
/// </remarks>
internal static class WorkspaceSocketRoot
{
    /// <summary>The directory this repository's tmux sockets live under.</summary>
    internal const string Root = "/tmp/libtmux-csharp-test";

    /// <summary>Points tmux and the temporary directory at <see cref="Root"/>.</summary>
    /// <remarks>
    /// Both variables are needed to cover both ways a socket gets located: tmux
    /// reads <c>TMUX_TMPDIR</c> when it execs and puts a <c>-L</c> socket under
    /// it, while a <c>-S</c> path ignores it entirely and is built here from the
    /// temporary directory, which is what <c>TMPDIR</c> moves.
    /// </remarks>
    [ModuleInitializer]
    internal static void Claim()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(Root);
        Environment.SetEnvironmentVariable("TMUX_TMPDIR", Root);
        Environment.SetEnvironmentVariable("TMPDIR", Root);
    }
}
