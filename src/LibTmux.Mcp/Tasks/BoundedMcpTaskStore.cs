using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

#pragma warning disable MCPEXP002

namespace LibTmux.Mcp;

/// <summary>Retains a bounded set of task results and admits bounded work.</summary>
/// <remarks>
/// The Tasks SDK calls <see cref="IMcpTaskStore.CreateTaskAsync" /> before it
/// queues the tool with <c>Task.Run</c>. Refusing here therefore bounds both
/// remembered tasks and background executions, rather than merely limiting
/// work after an unbounded queue already exists.
/// </remarks>
internal sealed class BoundedMcpTaskStore : IMcpTaskStore
{
    internal const int DefaultMaximumActiveTasks = 8;
    internal const int DefaultMaximumRetainedTasks = 256;
    internal const long DefaultPollIntervalMilliseconds = 1_000;
    internal static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromMinutes(15);

    private readonly object _gate = new();
    private readonly Dictionary<string, McpTaskInfo> _tasks = new(StringComparer.Ordinal);
    private readonly HashSet<string> _active = new(StringComparer.Ordinal);
    private readonly AsyncLocal<string?> _clientCancellation = new();
    private readonly TimeProvider _timeProvider;
    private readonly int _maximumActiveTasks;
    private readonly int _maximumRetainedTasks;
    private readonly TimeSpan _timeToLive;
    private readonly long _pollIntervalMilliseconds;

    internal BoundedMcpTaskStore(
        int maximumActiveTasks = DefaultMaximumActiveTasks,
        int maximumRetainedTasks = DefaultMaximumRetainedTasks,
        TimeSpan? timeToLive = null,
        long pollIntervalMilliseconds = DefaultPollIntervalMilliseconds,
        TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumActiveTasks);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            maximumRetainedTasks,
            maximumActiveTasks);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pollIntervalMilliseconds);

        TimeSpan retention = timeToLive ?? DefaultTimeToLive;
        if (retention <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeToLive),
                retention,
                "Task time-to-live must be positive.");
        }

        _maximumActiveTasks = maximumActiveTasks;
        _maximumRetainedTasks = maximumRetainedTasks;
        _timeToLive = retention;
        _pollIntervalMilliseconds = pollIntervalMilliseconds;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public event Action<InputResponseReceivedEventArgs>? InputResponseReceived;

    public Task<McpTaskInfo> CreateTaskAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        McpTaskInfo created;
        lock (_gate)
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            SweepExpired(now);
            if (_active.Count >= _maximumActiveTasks)
            {
                throw AtCapacity(
                    $"This server already has {_maximumActiveTasks} active MCP tasks. "
                    + "Wait for one to finish, or cancel one and wait for it to stop, "
                    + "before starting another.");
            }

            if (_tasks.Count >= _maximumRetainedTasks)
            {
                throw AtCapacity(
                    $"This server is retaining {_maximumRetainedTasks} MCP tasks. "
                    + $"They expire after {_timeToLive.TotalMinutes:g} minutes; retry "
                    + "after one expires, or restart this in-memory server.");
            }

            string taskId = Guid.NewGuid().ToString("N");
            created = new McpTaskInfo(
                taskId,
                McpTaskStatus.Working,
                now,
                now,
                _timeToLive,
                _pollIntervalMilliseconds);
            _tasks.Add(taskId, created);
            _active.Add(taskId);
        }

        return Task.FromResult(created);
    }

    public Task<McpTaskInfo?> GetTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            SweepExpired(_timeProvider.GetUtcNow());
            return Task.FromResult(_tasks.GetValueOrDefault(taskId));
        }
    }

    public Task SetCompletedAsync(
        string taskId,
        JsonElement result,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UpdateTerminal(taskId, McpTaskStatus.Completed, result, null);
        return Task.CompletedTask;
    }

    public Task SetFailedAsync(
        string taskId,
        JsonElement error,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UpdateTerminal(taskId, McpTaskStatus.Failed, null, error);
        return Task.CompletedTask;
    }

    public Task<bool> SetCancelledAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            SweepExpired(_timeProvider.GetUtcNow());
            if (!_tasks.TryGetValue(taskId, out McpTaskInfo? current))
            {
                return Task.FromResult(false);
            }

            bool clientCancellation = string.Equals(
                _clientCancellation.Value,
                taskId,
                StringComparison.Ordinal);
            if (IsTerminal(current.Status))
            {
                if (!clientCancellation && current.Status == McpTaskStatus.Cancelled)
                {
                    _active.Remove(taskId);
                }

                return Task.FromResult(false);
            }

            _tasks[taskId] = current with
            {
                Status = McpTaskStatus.Cancelled,
                LastUpdatedAt = _timeProvider.GetUtcNow(),
            };
            if (!clientCancellation)
            {
                _active.Remove(taskId);
            }

            return Task.FromResult(true);
        }
    }

    public Task ResolveInputRequestsAsync(
        string taskId,
        IDictionary<string, InputResponse> inputResponses,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentNullException.ThrowIfNull(inputResponses);
        cancellationToken.ThrowIfCancellationRequested();
        bool notify = false;
        lock (_gate)
        {
            McpTaskInfo current = Require(taskId);
            if (!IsTerminal(current.Status))
            {
                ImmutableDictionary<string, InputRequest> requests = current.InputRequests
                    ?.ToImmutableDictionary(StringComparer.Ordinal)
                    ?? ImmutableDictionary<string, InputRequest>.Empty
                        .WithComparers(StringComparer.Ordinal);
                foreach (string requestId in inputResponses.Keys)
                {
                    requests = requests.Remove(requestId);
                }

                _tasks[taskId] = current with
                {
                    InputRequests = requests,
                    Status = requests.IsEmpty
                        ? McpTaskStatus.Working
                        : McpTaskStatus.InputRequired,
                    LastUpdatedAt = _timeProvider.GetUtcNow(),
                };
                notify = true;
            }
        }

        if (notify)
        {
            foreach ((string requestId, InputResponse response) in inputResponses)
            {
                InputResponseReceived?.Invoke(new InputResponseReceivedEventArgs
                {
                    TaskId = taskId,
                    RequestId = requestId,
                    Response = response,
                });
            }
        }

        return Task.CompletedTask;
    }

    public Task SetInputRequestsAsync(
        string taskId,
        IDictionary<string, InputRequest> inputRequests,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentNullException.ThrowIfNull(inputRequests);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            McpTaskInfo current = Require(taskId);
            if (!IsTerminal(current.Status))
            {
                ImmutableDictionary<string, InputRequest> requests = current.InputRequests
                    ?.ToImmutableDictionary(StringComparer.Ordinal)
                    ?? ImmutableDictionary<string, InputRequest>.Empty
                        .WithComparers(StringComparer.Ordinal);
                foreach ((string requestId, InputRequest request) in inputRequests)
                {
                    requests = requests.SetItem(requestId, request);
                }

                _tasks[taskId] = current with
                {
                    InputRequests = requests,
                    Status = McpTaskStatus.InputRequired,
                    LastUpdatedAt = _timeProvider.GetUtcNow(),
                };
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsTerminal(McpTaskStatus status) =>
        status is McpTaskStatus.Completed or McpTaskStatus.Failed or McpTaskStatus.Cancelled;

    private void UpdateTerminal(
        string taskId,
        McpTaskStatus status,
        JsonElement? result,
        JsonElement? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        lock (_gate)
        {
            McpTaskInfo current = Require(taskId);
            if (!IsTerminal(current.Status))
            {
                _tasks[taskId] = current with
                {
                    Status = status,
                    Result = result,
                    Error = error,
                    LastUpdatedAt = _timeProvider.GetUtcNow(),
                };
            }

            // SDK background finalization reaches these setters even when a
            // client cancellation already made the record terminal-visible.
            _active.Remove(taskId);
        }
    }

    private McpTaskInfo Require(string taskId)
    {
        SweepExpired(_timeProvider.GetUtcNow());
        return _tasks.TryGetValue(taskId, out McpTaskInfo? task)
            ? task
            : throw new InvalidOperationException($"Task '{taskId}' not found.");
    }

    private void SweepExpired(DateTimeOffset now)
    {
        foreach ((string taskId, McpTaskInfo task) in _tasks.ToArray())
        {
            if (now - task.CreatedAt < _timeToLive)
            {
                continue;
            }

            if (!_active.Contains(taskId))
            {
                _tasks.Remove(taskId);
            }
        }
    }

    internal IDisposable EnterClientCancellation(string taskId)
    {
        string? previous = _clientCancellation.Value;
        _clientCancellation.Value = taskId;
        return new CancellationScope(_clientCancellation, previous);
    }

    private static McpProtocolException AtCapacity(string message) =>
        new(message, McpErrorCode.InvalidRequest);

    private sealed class CancellationScope(
        AsyncLocal<string?> current,
        string? previous) : IDisposable
    {
        public void Dispose() => current.Value = previous;
    }
}

/// <summary>Marks calls made by the SDK's client-cancellation handler.</summary>
internal sealed class BoundedMcpTaskCancellationOptions(
    BoundedMcpTaskStore store) : IConfigureOptions<McpServerOptions>
{
    private const string CancelMethod = "tasks/cancel";

    public void Configure(McpServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        IList<McpServerRequestHandler> handlers = options.RequestHandlers
            ?? throw MissingHandler();
        int[] matches = handlers
            .Select((handler, index) => (handler, index))
            .Where(static candidate => candidate.handler.Method == CancelMethod)
            .Select(static candidate => candidate.index)
            .ToArray();
        if (matches.Length != 1)
        {
            throw MissingHandler();
        }

        int index = matches[0];
        McpServerRequestHandler inner = handlers[index];
        handlers[index] = new McpServerRequestHandler
        {
            Method = inner.Method,
            RoutingNameParameter = inner.RoutingNameParameter,
            Handler = async (request, cancellationToken) =>
            {
                string taskId = request.Params?["taskId"]?.GetValue<string>() ?? string.Empty;
                using IDisposable scope = store.EnterClientCancellation(taskId);
                return await inner.Handler(request, cancellationToken).ConfigureAwait(false);
            },
        };
    }

    private static InvalidOperationException MissingHandler() =>
        new("The MCP Tasks SDK did not register exactly one tasks/cancel handler.");
}
