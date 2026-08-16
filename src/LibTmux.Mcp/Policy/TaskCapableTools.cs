using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <summary>Decides which tools a client may run as a task rather than a call.</summary>
/// <remarks>
/// <para>
/// The Tasks extension lets a client start a tool call, get a handle back
/// immediately, and collect the result later — the protocol's own answer to
/// the problem <c>tmux_start_job</c> solves by hand. Where both exist the
/// protocol's is better: the client drives it, so a model does not have to
/// remember a handle across turns.
/// </para>
/// <para>
/// Only the tools that wait are offered this way. A listing answers in
/// milliseconds, and turning it into a task would cost a second round trip to
/// collect an answer that was already there.
/// </para>
/// <para>
/// Every tool is <see cref="McpTaskExecutionMode.Optional" />, never
/// <c>Required</c>: a client that has not declared the extension keeps working
/// exactly as before, and one that has gets the handle. Requiring it would
/// break every client that has not implemented an extension yet.
/// </para>
/// </remarks>
internal static class TaskCapableTools
{
    /// <summary>The tools whose whole job is to wait for something.</summary>
    private static readonly HashSet<string> Waiting = new(StringComparer.Ordinal)
    {
        "tmux_run",
        "tmux_wait_for_text",
        "tmux_wait_for_channel",
        "tmux_job",
    };

    /// <summary>Answers how one tool call may be executed.</summary>
    /// <param name="request">The call being routed.</param>
    /// <returns>Whether the call may become a task.</returns>
    internal static McpTaskExecutionMode Select(RequestContext<CallToolRequestParams> request) =>
        request?.Params?.Name is string name && Waiting.Contains(name)
            ? McpTaskExecutionMode.Optional
            : McpTaskExecutionMode.Synchronous;
}
