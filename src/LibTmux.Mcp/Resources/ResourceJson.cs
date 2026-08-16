using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace LibTmux.Mcp;

/// <summary>Serializes what a resource answers, without reflecting to do it.</summary>
/// <remarks>
/// Source-generated rather than reflective. The reflective overload is the
/// only thing in this server that a trimmer or an ahead-of-time compiler
/// cannot follow, and the failure it produces is the worst kind: a resource
/// that reads correctly in a normal build and answers an empty object in a
/// published one, because the properties it needed were trimmed away.
/// </remarks>
[UnsupportedOSPlatform("windows")]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(HierarchyView))]
[JsonSerializable(typeof(IReadOnlyList<SessionInfo>))]
[JsonSerializable(typeof(IReadOnlyList<WindowInfo>))]
[JsonSerializable(typeof(IReadOnlyList<PaneInfo>))]
[JsonSerializable(typeof(IReadOnlyList<DiscoveredServer>))]
[JsonSerializable(typeof(PaneInfo))]
internal sealed partial class ResourceJson : JsonSerializerContext
{
    /// <summary>Renders a value as the JSON a resource answers with.</summary>
    /// <typeparam name="T">What is being rendered.</typeparam>
    /// <param name="value">The value, which may be null.</param>
    /// <param name="type">The generated type information for it.</param>
    /// <returns>The JSON text.</returns>
    /// <remarks>
    /// A resource that answers nothing still has to answer something: the SDK
    /// treats a null result as a fault rather than as an empty reading, so a
    /// null becomes JSON <c>null</c> here.
    /// </remarks>
    internal static string Render<T>(T? value, JsonTypeInfo<T> type)
        where T : class =>
        value is null ? "null" : JsonSerializer.Serialize(value, type);
}
