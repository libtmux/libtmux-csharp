using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol;

namespace LibTmux.Mcp;

/// <summary>Keeps structured tool results faithful to their advertised schemas.</summary>
internal static class ToolJson
{
    /// <summary>Serializes required nullable properties instead of dropping them.</summary>
    internal static JsonSerializerOptions Options { get; } = new(McpJsonUtilities.DefaultOptions)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
}
