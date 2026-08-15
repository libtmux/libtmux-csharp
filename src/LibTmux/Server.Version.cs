namespace LibTmux;

/// <summary>Provides captured tmux version metadata.</summary>
public sealed partial class Server
{
    /// <summary>Gets the captured tmux version.</summary>
    public TmuxVersion? Version
    {
        get
        {
            const string prefix = "tmux ";
            if (RawVersion is null
                || !RawVersion.StartsWith(prefix, StringComparison.Ordinal)
                || !TmuxVersion.TryParse(RawVersion[prefix.Length..], out TmuxVersion version)
                || string.Equals(version.Suffix, "next", StringComparison.Ordinal))
            {
                return null;
            }

            return version;
        }
    }
}
