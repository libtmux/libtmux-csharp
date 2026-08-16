using System.Reflection;

namespace LibTmux.Mcp;

/// <summary>What this server says it is.</summary>
public static class LibTmuxMcp
{
    /// <summary>Gets the version reported to a client.</summary>
    /// <remarks>
    /// An assembly version is four numbers, so a prerelease would arrive at the
    /// client as "0.0.0.0" — which reads as unset rather than as an alpha. The
    /// informational version is the one the package carries, and the build
    /// metadata after "+" is a commit nobody asked for.
    /// </remarks>
    public static string Version { get; } = Resolve();

    private static string Resolve()
    {
        string? informational = typeof(LibTmuxMcp).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (string.IsNullOrEmpty(informational))
        {
            return typeof(LibTmuxMcp).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        }

        int metadata = informational.IndexOf('+', StringComparison.Ordinal);
        return metadata < 0 ? informational : informational[..metadata];
    }
}
