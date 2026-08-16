using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol;

namespace LibTmux.Mcp;

/// <summary>Where a reader of a pane left off.</summary>
/// <remarks>
/// <para>
/// A pane is a grid tmux rewrites in place, not a log that only grows, so
/// "since last time" cannot be a line number alone. The anchor is an absolute
/// position <em>and</em> a fingerprint of the rows at it: the position finds
/// the place quickly, and the fingerprint proves it is still the same place.
/// </para>
/// <para>
/// Opaque to callers on purpose. A cursor built by hand would encode
/// assumptions about a grid that tmux is free to change underneath it.
/// </para>
/// </remarks>
[UnsupportedOSPlatform("windows")]
internal sealed record TailCursor(
    string PaneId,
    string PanePid,
    int HistorySize,
    int PaneHeight,
    int AnchorAbsolute,
    string? AnchorHash,
    IReadOnlyList<string> BelowHashes)
{
    private const string Prefix = "tmux-tail-v1:";

    /// <summary>Fingerprints one row.</summary>
    /// <param name="line">The row's text.</param>
    /// <returns>A stable hash of it.</returns>
    internal static string HashLine(string line)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(line));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    /// <summary>Builds a cursor for where a read finished.</summary>
    /// <param name="paneId">The pane that was read.</param>
    /// <param name="state">The grid state the read saw.</param>
    /// <param name="cursorRows">The rows from the cursor row to the visible bottom.</param>
    /// <returns>The cursor.</returns>
    internal static TailCursor Build(
        string paneId,
        PaneGridState state,
        IReadOnlyList<string> cursorRows) =>
        new(
            PaneId: paneId,
            PanePid: state.PanePid,
            HistorySize: state.HistorySize,
            PaneHeight: state.PaneHeight,
            AnchorAbsolute: state.CursorAbsolute,
            AnchorHash: cursorRows.Count > 0 ? HashLine(cursorRows[0]) : null,
            BelowHashes: [.. cursorRows.Skip(1).Select(HashLine)]);

    /// <summary>Renders the cursor as the opaque token a caller passes back.</summary>
    /// <returns>The token.</returns>
    public string Encode()
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(this, TailCursorJson.Default.TailCursor);
        return Prefix + ToBase64Url(json);
    }

    // Hand-rolled rather than System.Buffers.Text.Base64Url, which arrived in
    // .NET 9 and this tool still targets .NET 8.
    private static string ToBase64Url(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight((padded.Length + 3) / 4 * 4, '='));
    }

    /// <summary>Reads a token a caller passed back.</summary>
    /// <param name="token">The token, or null when the caller sent none.</param>
    /// <returns>The cursor, or null when there was no token.</returns>
    /// <exception cref="McpException">The token was not one this server issued.</exception>
    public static TailCursor? Decode(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        string trimmed = token.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new McpException(
                "That is not a tmux_tail_pane cursor. Pass back the cursor from the "
                + "previous call, or omit it to start from what is on screen now.");
        }

        try
        {
            byte[] json = FromBase64Url(trimmed[Prefix.Length..]);
            return JsonSerializer.Deserialize(json, TailCursorJson.Default.TailCursor)
                ?? throw new McpException("That tmux_tail_pane cursor is empty.");
        }
        catch (Exception error) when (error is FormatException or JsonException)
        {
            throw new McpException(
                "That tmux_tail_pane cursor is damaged. Omit it to start from what is "
                + "on screen now.");
        }
    }
}

/// <summary>Serializes cursors without reflection, so the tool can be trimmed.</summary>
[JsonSerializable(typeof(TailCursor))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class TailCursorJson : JsonSerializerContext;
