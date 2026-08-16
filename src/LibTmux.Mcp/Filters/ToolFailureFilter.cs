using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <summary>Turns a failure into something the caller can act on.</summary>
/// <remarks>
/// <para>
/// An unhandled exception reaches the client as "An error occurred invoking
/// 'tmux_run'" — true, and useless. A model reading that has no way to tell a
/// dead pane from a missing binary from its own bad argument, so it retries
/// the same call. Every failure here names what went wrong and what to do
/// instead.
/// </para>
/// <para>
/// It also retries once when the tmux server was replaced underneath a cached
/// handle. That happens whenever tmux is restarted between calls, and it is
/// not something a caller can be expected to understand, let alone fix.
/// </para>
/// </remarks>
[UnsupportedOSPlatform("windows")]
internal static class ToolFailureFilter
{
    /// <summary>Builds the filter.</summary>
    /// <returns>The filter.</returns>
    /// <remarks>
    /// Everything it needs comes off the request rather than being captured,
    /// because the filter is composed while the container is still being built
    /// — capturing a second accessor here would give it a cache nothing else
    /// writes to, and invalidating that would fix nothing.
    /// </remarks>
    internal static McpRequestFilter<CallToolRequestParams, CallToolResult> Create() =>
        next => async (request, cancellationToken) =>
    {
        string tool = request.Params?.Name ?? "a tmux tool";
        ILogger logger = request.Services?.GetService<ILoggerFactory>()
                ?.CreateLogger(nameof(ToolFailureFilter))
            ?? NullLogger.Instance;
        try
        {
            return await next(request, cancellationToken).ConfigureAwait(false);
        }
        catch (StaleServerGenerationException)
        {
            // The socket now holds a different tmux process. Forgetting the
            // handle and asking again is exactly what a caller would have to
            // do, and they have no way to know that.
            request.Services?.GetService<TmuxConnectionAccessor>()
                ?.Invalidate(SocketArgument(request));
            try
            {
                return await next(request, cancellationToken).ConfigureAwait(false);
            }
            catch (LibTmuxException retried)
            {
                return Failure(
                    logger,
                    tool,
                    retried,
                    "The tmux server was restarted and the retry failed too. "
                    + "Call tmux_list_servers to see what is running now.");
            }
        }
        catch (TmuxVersionTooLowException error)
        {
            return Failure(
                logger,
                tool,
                error,
                "This tmux is too old for that operation. "
                + "Call tmux_server_info to see which version is running.");
        }
        catch (TmuxObjectNotFoundException error)
        {
            return Failure(
                logger,
                tool,
                error,
                "That session, window or pane no longer exists. "
                + "Call tmux_hierarchy to see what does.");
        }
        catch (TmuxCommandException error)
        {
            // tmux's own message is the most specific thing anybody has.
            return Failure(logger, tool, error, $"tmux refused the command: {error.Message}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(
                logger,
                tool,
                null,
                "The operation ran out of time. Nothing was rolled back — whatever "
                + "was started is still running in its pane. Read the pane before "
                + "trying again, so you do not start it twice.");
        }
        catch (LibTmuxException error)
        {
            return Failure(logger, tool, error, error.Message);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The backstop. Anything unhandled reaches a client as "An error
            // occurred invoking 'tmux_run'", which is true and unusable: a
            // model cannot tell a bug from its own bad argument, so it retries
            // the same call until the turn is gone. Naming the tool and the
            // message costs nothing and ends that loop.
            return Failure(
                logger,
                tool,
                error,
                $"{error.Message} This is unexpected — check the server's log on "
                + "standard error before retrying, because retrying unchanged will "
                + "most likely fail the same way.");
        }
    };

    private static string? SocketArgument(RequestContext<CallToolRequestParams> request) =>
        request.Params?.Arguments is { } arguments
            && arguments.TryGetValue("socketName", out System.Text.Json.JsonElement socket)
            && socket.ValueKind == System.Text.Json.JsonValueKind.String
                ? socket.GetString()
                : null;

    private static CallToolResult Failure(
        ILogger logger,
        string tool,
        Exception? error,
        string advice)
    {
        if (error is not null)
        {
            Log.ToolFailed(logger, error, tool);
        }

        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = $"{tool} failed. {advice}" }],
        };
    }
}
