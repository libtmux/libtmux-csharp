namespace LibTmux.Internal;

/// <summary>Owns the exact psmux build and endpoint spellings this preview accepts.</summary>
internal static class PsmuxCompatibility
{
    internal const string SupportedVersion = "3.3.7";
    internal const string SupportedCommit =
        "aa26cd39edcfab03e718f94ea21bb47e8c5b85e8";
    internal const string SupportedBinarySha256 =
        "1abd0eaa3de1ed5491a4f744c8b3db492ae9ac94e9e9a8fea9da217c744ba94e";
    internal const string SupportedImplementationLine =
        "psmux 3.3.7 (aa26cd3 2026-08-17)";

    internal static string ValidateExpectedBinarySha256(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length != 64
            || value.Any(static character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException(
                "The expected psmux binary SHA-256 must contain exactly 64 hexadecimal characters.",
                parameterName);
        }

        string normalized = value.ToLowerInvariant();
        if (!string.Equals(normalized, SupportedBinarySha256, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The expected psmux binary SHA-256 must match the exact audited build.",
                parameterName);
        }

        return normalized;
    }

    internal static string NormalizeDataDirectory(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.IndexOfAny(['\0', '\r', '\n']) >= 0)
        {
            throw new ArgumentException(
                "The psmux data directory cannot contain NUL, CR, or LF characters.",
                parameterName);
        }

        string path = value.Replace('/', '\\');
        if (path.Length >= 3
            && char.IsAsciiLetter(path[0])
            && path[1] == ':'
            && path[2] == '\\')
        {
            string root = $"{char.ToUpperInvariant(path[0])}:\\";
            EnsureNativeFixedDrive(root, parameterName);
            return root + NormalizeSegments(path[3..], parameterName).ToLowerInvariant();
        }

        throw new ArgumentException(
            "The psmux data directory must be an absolute path on a local Windows drive.",
            parameterName);
    }

    internal static void EnsureNativeFixedDrive(string path, string parameterName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string? root = Path.GetPathRoot(path);
        DriveType driveType;
        try
        {
            driveType = string.IsNullOrEmpty(root)
                ? DriveType.Unknown
                : new DriveInfo(root).DriveType;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException(
                "The psmux path must use an accessible fixed local Windows drive.",
                parameterName,
                error);
        }

        if (driveType != DriveType.Fixed)
        {
            throw new ArgumentException(
                "The psmux path must use a fixed local Windows drive.",
                parameterName);
        }
    }

    internal static string ValidateNamespaceName(string value, string parameterName)
    {
        ValidateName(value, "namespace", parameterName);
        if (value.Any(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException(
                "The psmux namespace must use lowercase ASCII spelling.",
                parameterName);
        }

        if (string.Equals(value, "default", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The psmux preview does not use the ambiguous default namespace.",
                parameterName);
        }

        if (value.Length is < 16 or > 64)
        {
            throw new ArgumentException(
                "The psmux namespace must contain between 16 and 64 characters.",
                parameterName);
        }

        return value;
    }

    internal static string ValidateName(string value, string kind, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.IndexOfAny(['\0', '\r', '\n']) >= 0
            || value.Contains("__", StringComparison.Ordinal)
            || value.Any(static character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
        {
            throw new ArgumentException(
                $"psmux {kind} names must use only ASCII letters, digits, '-' or '_' and cannot contain '__'.",
                parameterName);
        }

        return value;
    }

    private static string NormalizeSegments(string value, string parameterName)
    {
        string[] segments = value.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new ArgumentException(
                "The psmux data directory cannot be a filesystem root.",
                parameterName);
        }

        foreach (string segment in segments)
        {
            string stem = segment.Split('.', 2)[0].ToUpperInvariant();
            bool reservedDevice = stem is "CON" or "PRN" or "AUX" or "NUL"
                || (stem.Length == 4
                    && stem[3] is >= '1' and <= '9'
                    && stem[..3] is "COM" or "LPT");
            if (segment is "." or ".."
                || segment.EndsWith(' ')
                || segment.EndsWith('.')
                || segment.Any(static character => character < ' ')
                || segment.IndexOfAny(['<', '>', ':', '"', '|', '?', '*']) >= 0
                || reservedDevice)
            {
                throw new ArgumentException(
                    "The psmux data directory must use canonical Windows segments without reserved names or characters.",
                    parameterName);
            }
        }

        return string.Join('\\', segments);
    }
}
