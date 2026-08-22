using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <summary>Refuses a serialized resource result that exceeds policy.</summary>
internal static class ResourceResponseBudgetFilter
{
    /// <summary>Builds the resource-response budget filter.</summary>
    internal static McpRequestFilter<ReadResourceRequestParams, ReadResourceResult> Create(
        ServerPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return next => async (request, cancellationToken) =>
        {
            ReadResourceResult result = await next(request, cancellationToken)
                .ConfigureAwait(false);
            int applicationBudget = policy.MaxBytes - Utf8JsonBudget.ProtocolMetadataReserve;
            if (applicationBudget > 0
                && Utf8JsonBudget.Fits(result, applicationBudget, ToolJson.Options))
            {
                return result;
            }

            throw new McpException(
                $"The resource response exceeded this server's {policy.MaxBytes} UTF-8 byte "
                + "limit. Read a narrower resource or raise "
                + $"{ServerPolicy.MaxBytesVariable} and restart the MCP server.");
        };
    }
}
