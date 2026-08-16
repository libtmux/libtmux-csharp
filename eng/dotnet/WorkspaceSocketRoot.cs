using System.Runtime.CompilerServices;

namespace LibTmux.Engineering;

/// <summary>Keeps this repository's tmux out of the socket root other ports share.</summary>
/// <remarks>
/// Every libtmux port on this machine can reach the default socket root, so
/// one port's cleanup can kill another's servers mid-run. This must run before
/// the first server starts, because claiming the root after does not move it.
/// </remarks>
internal static class WorkspaceSocketRoot
{
    /// <summary>The directory this repository's tmux sockets live under.</summary>
    internal const string Root = "/tmp/libtmux-dotnet-test";

    /// <summary>Points tmux and the temporary directory at <see cref="Root"/>.</summary>
    /// <remarks>
    /// Covers both socket routes: tmux reads <c>TMUX_TMPDIR</c> for a <c>-L</c>
    /// socket, while a <c>-S</c> path ignores it and is built from <c>TMPDIR</c> instead.
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
