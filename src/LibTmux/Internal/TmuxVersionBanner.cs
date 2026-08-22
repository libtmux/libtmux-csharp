namespace LibTmux.Internal;

internal readonly record struct TmuxVersionBanner(
    TmuxImplementation Implementation,
    string RawVersion,
    string Version,
    string? ImplementationLine);

internal static class TmuxVersionBannerParser
{
    internal static bool TryParse(
        IReadOnlyList<string> lines,
        out TmuxVersionBanner banner)
    {
        banner = default;
        if (lines.Count is < 1 or > 2)
        {
            return false;
        }

        string first = lines[0];
        if (!first.StartsWith("tmux ", StringComparison.Ordinal)
            || !TmuxVersion.TryParse(first[5..], out TmuxVersion version))
        {
            return false;
        }

        if (lines.Count == 1)
        {
            banner = new TmuxVersionBanner(
                TmuxImplementation.Tmux,
                first,
                version.Raw,
                ImplementationLine: null);
            return true;
        }

        string prefix = $"psmux {version.Raw}";
        string second = lines[1];
        if (!second.StartsWith(prefix, StringComparison.Ordinal)
            || (second.Length != prefix.Length
                && !(second.Length > prefix.Length + 3
                    && second[prefix.Length] == ' '
                    && second[prefix.Length + 1] == '('
                    && second[^1] == ')')))
        {
            return false;
        }

        banner = new TmuxVersionBanner(
            TmuxImplementation.Psmux,
            first,
            version.Raw,
            second);
        return true;
    }
}
