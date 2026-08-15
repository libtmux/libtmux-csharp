using System.Collections.Frozen;

namespace LibTmux.Internal;

internal sealed record FormatFieldDescriptor
{
    internal FormatFieldDescriptor(
        string wireName,
        string clrMemberName,
        TmuxVersion minimumTmuxVersion,
        IReadOnlySet<string> scopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wireName);
        ArgumentException.ThrowIfNullOrWhiteSpace(clrMemberName);
        if (!minimumTmuxVersion.IsValid)
        {
            throw new ArgumentException(
                "A valid minimum tmux version is required.",
                nameof(minimumTmuxVersion));
        }

        ArgumentNullException.ThrowIfNull(scopes);
        string[] scopeCopy = [.. scopes];
        if (scopeCopy.Length == 0
            || scopeCopy.Any(static scope => string.IsNullOrWhiteSpace(scope)))
        {
            throw new ArgumentException(
                "At least one nonblank format scope is required.",
                nameof(scopes));
        }

        WireName = wireName;
        ClrMemberName = clrMemberName;
        MinimumTmuxVersion = minimumTmuxVersion;
        Scopes = scopeCopy.ToFrozenSet(StringComparer.Ordinal);
    }

    internal string WireName { get; }

    internal string ClrMemberName { get; }

    internal TmuxVersion MinimumTmuxVersion { get; }

    internal IReadOnlySet<string> Scopes { get; }
}
