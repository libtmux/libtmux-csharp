using System.Globalization;

namespace LibTmux.Mcp;

/// <summary>Reads tmux's format fields out of an entity's captured snapshot.</summary>
/// <remarks>
/// tmux answers every field as text and omits one it does not recognise, which
/// is how a field added in a later release behaves on an older one. Every
/// reader here therefore answers a default rather than throwing: a missing
/// field means "this tmux does not report it", not "the capture is broken".
/// </remarks>
internal static class FormatFields
{
    internal static string? Text(IReadOnlyDictionary<string, string?> fields, string name) =>
        fields.TryGetValue(name, out string? value) && !string.IsNullOrEmpty(value) ? value : null;

    internal static bool Flag(IReadOnlyDictionary<string, string?> fields, string name) =>
        string.Equals(Text(fields, name), "1", StringComparison.Ordinal);

    internal static int? Number(IReadOnlyDictionary<string, string?> fields, string name) =>
        int.TryParse(Text(fields, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;
}
