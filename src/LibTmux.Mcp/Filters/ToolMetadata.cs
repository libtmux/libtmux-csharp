using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

internal static class ToolMetadata
{
    internal static bool MayModify(
        RequestContext<CallToolRequestParams> request,
        string tool)
    {
        McpServerOptions? options = request.Services
            ?.GetService<IOptions<McpServerOptions>>()?.Value;
        McpServerTool? registered = options?.ToolCollection?
            .FirstOrDefault(candidate => string.Equals(
                candidate.ProtocolTool.Name,
                tool,
                StringComparison.Ordinal));
        return registered?.ProtocolTool.Annotations?.ReadOnlyHint != true;
    }
}
