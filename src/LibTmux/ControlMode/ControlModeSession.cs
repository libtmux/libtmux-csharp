using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading.Channels;
using LibTmux.Internal;

namespace LibTmux;

/// <summary>Reads one tmux control client and correlates what it says.</summary>
/// <remarks>
/// tmux answers on one stream that carries two different things: blocks that
/// answer a command, and notifications nobody asked for. A single reader owns
/// the stream and splits them, because two readers on one pipe would interleave
/// and neither would see a whole block.
/// </remarks>
[UnsupportedOSPlatform("windows")]
internal sealed class ControlModeSession : IControlModeSession
{
    private readonly Process _process;
    /// <summary>How many unread events are held before the oldest are dropped.</summary>
    /// <remarks>
    /// A pane can outpace any reader, and a caller may never read
    /// <see cref="Events"/> at all, so unbounded buffering has no ceiling.
    /// The channel drops the oldest event instead of blocking, since blocking
    /// would also stall the reader that completes commands.
    /// </remarks>
    internal const int EventBufferCapacity = 4096;

    private readonly Channel<TmuxEvent> _events =
        Channel.CreateBounded<TmuxEvent>(new BoundedChannelOptions(EventBufferCapacity)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

    private readonly Queue<PendingCommand> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly TaskCompletionSource _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Task _pump;
    private bool _disposed;

    /// <summary>How long disposal waits for the client to exit before killing it.</summary>
    /// <remarks>
    /// Closing stdin asks tmux to leave. A client that does not answer -- wedged,
    /// stopped, or waiting on something -- would otherwise hang the caller's
    /// disposal forever, and disposal is the one operation that has to finish.
    /// </remarks>
    private static readonly TimeSpan ExitBudget = TimeSpan.FromSeconds(5);

    private ControlModeSession(Process process)
    {
        _process = process;
        _pump = Task.Run(PumpAsync);
    }

    public IAsyncEnumerable<TmuxEvent> Events => _events.Reader.ReadAllAsync();

    public bool IsRunning => !_process.HasExited;

    [UnsupportedOSPlatform("windows")]
    internal static ControlModeSession Start(
        string tmuxBinaryPath,
        IReadOnlyList<string> prefixArguments,
        string? target,
        Action<ProcessStartInfo> configureEnvironment)
    {
        // Stderr is intentionally left undrained: a tmux client can hand its
        // write end to the server it starts, so the pipe can outlive the client
        // and a reader on it never observes cancellation on Unix. Waiting for
        // the server to exit, or closing the handle mid-read, would both hang
        // disposal instead.
        //
        // The residual risk is a client blocking on a full stderr pipe. It
        // writes to stderr only when tmux itself fails to start -- at most
        // kilobytes, followed by the process exiting.
        ProcessStartInfo startInfo = new(tmuxBinaryPath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in prefixArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add("-C");

        // Attaching is not decoration. A control client that never attaches is
        // told about the hierarchy but not about pane output, so %output never
        // arrives and the stream looks mysteriously quiet.
        startInfo.ArgumentList.Add("attach-session");
        if (target is not null)
        {
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(target);
        }

        configureEnvironment(startInfo);
        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The tmux control client did not start.");
        return new ControlModeSession(process);
    }

    /// <summary>Waits until tmux has answered its own attach.</summary>
    internal Task WaitForReadyAsync(CancellationToken cancellationToken) =>
        _ready.Task.WaitAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> SendAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(command);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_process.HasExited)
        {
            throw new InvalidOperationException("The tmux control client has exited.");
        }

        PendingCommand pending = new();

        // Queueing and writing happen together under one lock. tmux answers in
        // the order it was asked, so a caller that queued second and wrote
        // first would be handed the other caller's answer.
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Queued before the write: a command such as kill-server can end
            // the client as its own answer, and the pump's exit sweep must
            // find this waiter already queued to fail it.
            lock (_pending)
            {
                _pending.Enqueue(pending);
            }

            try
            {
                await _process.StandardInput.WriteLineAsync(command.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // tmux never saw this command, so it will never answer it. The
                // slot is marked abandoned rather than removed, so replies skip
                // it instead of being handed to the wrong caller.
                pending.Abandon();
                throw;
            }
        }
        finally
        {
            _writeLock.Release();
        }

        return await pending.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (!_process.HasExited)
            {
                _process.StandardInput.Close();
                using CancellationTokenSource budget = new(ExitBudget);
                try
                {
                    await _process.WaitForExitAsync(budget.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Asking did not work, so stop asking. A disposal that never
                    // returns is worse than a client that did not shut down
                    // politely.
                    //
                    // Kills only the client, not its process tree: the server
                    // underneath may still be serving other clients.
                    _process.Kill(entireProcessTree: false);
                    await _process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
        }
        catch (InvalidOperationException)
        {
            // The client raced us to exit, which is the state we wanted anyway.
        }
        finally
        {
            await _pump.ConfigureAwait(false);
            _process.Dispose();
            _writeLock.Dispose();
        }
    }

    /// <summary>One waiting command, and whether tmux ever heard it.</summary>
    private sealed class PendingCommand
    {
        internal TaskCompletionSource<IReadOnlyList<string>> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool IsAbandoned { get; private set; }

        internal void Abandon() => IsAbandoned = true;
    }

    private async Task PumpAsync()
    {
        string? exitReason = null;
        try
        {
            while (await _process.StandardOutput.ReadLineAsync().ConfigureAwait(false)
                is string line)
            {
                if (line.StartsWith("%begin ", StringComparison.Ordinal))
                {
                    await ReadBlockAsync(line).ConfigureAwait(false);
                    continue;
                }

                if (!line.StartsWith('%'))
                {
                    // tmux prints nothing outside a block that is not a
                    // notification, so anything here is a protocol the reader
                    // does not know rather than data to guess at.
                    continue;
                }

                (string name, IReadOnlyList<string> arguments) = SplitNotification(line);
                if (string.Equals(name, "exit", StringComparison.Ordinal))
                {
                    exitReason = arguments.Count == 0 ? null : string.Join(' ', arguments);
                    break;
                }

                _events.Writer.TryWrite(ToEvent(name, arguments));
            }
        }
        finally
        {
            _events.Writer.TryWrite(new TmuxExitEvent(exitReason));
            _events.Writer.TryComplete();
            _ready.TrySetException(new InvalidOperationException(
                "The tmux control client exited before it finished attaching."));
            FailPending();
        }
    }

    private async Task ReadBlockAsync(string beginLine)
    {
        // A block ends only at %end or %error carrying the same numbers the
        // %begin did. Stopping at the first line that starts with a percent
        // would truncate a block whose own output starts with one, and a pane
        // id such as %0 does exactly that.
        string suffix = beginLine["%begin ".Length..];
        List<string> lines = [];
        bool failed = false;

        while (await _process.StandardOutput.ReadLineAsync().ConfigureAwait(false)
            is string line)
        {
            if (IsBlockTerminator(line, "%end ", suffix))
            {
                break;
            }

            if (IsBlockTerminator(line, "%error ", suffix))
            {
                failed = true;
                break;
            }

            lines.Add(line);
        }

        // Attaching is itself a command, so tmux answers it before any caller
        // has asked. Handing that block to the first caller would answer every
        // command with the previous one's output for the life of the session,
        // and waiting for "nobody is queued" loses the race against a caller
        // that sends immediately.
        if (_ready.TrySetResult())
        {
            return;
        }

        TaskCompletionSource<IReadOnlyList<string>>? completion;
        lock (_pending)
        {
            // Abandoned slots belong to commands that were never sent, so tmux
            // is not answering them. Skipping them here is what keeps replies
            // aligned with the commands that actually reached it.
            completion = null;
            while (_pending.Count > 0)
            {
                PendingCommand candidate = _pending.Dequeue();
                if (!candidate.IsAbandoned)
                {
                    completion = candidate.Completion;
                    break;
                }
            }
        }

        if (completion is null)
        {
            return;
        }

        if (failed)
        {
            completion.TrySetException(new TmuxCommandException(
                lines.Count == 0 ? "The tmux command failed." : string.Join('\n', lines),
                BuildFailure(lines)));
            return;
        }

        completion.TrySetResult(lines);
    }

    private static TmuxCommandResult BuildFailure(IReadOnlyList<string> lines)
    {
        // tmux reports a control-mode failure as the block's own lines rather
        // than on a separate stream, so they are the error text here.
        byte[] text = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', lines));
        return new TmuxCommandResult(
            arguments: [],
            exitCode: 1,
            standardOutput: ReadOnlyMemory<byte>.Empty,
            standardError: text,
            standardOutputLines: [],
            standardErrorLines: lines);
    }

    private static bool IsBlockTerminator(string line, string prefix, string suffix) =>
        line.StartsWith(prefix, StringComparison.Ordinal)
        && string.Equals(line[prefix.Length..], suffix, StringComparison.Ordinal);

    private static (string Name, IReadOnlyList<string> Arguments) SplitNotification(string line)
    {
        string body = line[1..];
        int separator = body.IndexOf(' ', StringComparison.Ordinal);
        return separator < 0
            ? (body, [])
            : (body[..separator], body[(separator + 1)..].Split(' '));
    }

    private static TmuxEvent ToEvent(string name, IReadOnlyList<string> arguments)
    {
        if (!string.Equals(name, "output", StringComparison.Ordinal) || arguments.Count == 0)
        {
            return new TmuxNotificationEvent(name, arguments);
        }

        // Only the pane id is a word. Everything after the first space is the
        // payload, which may hold spaces of its own and is escaped the way tmux
        // escapes an option value.
        string payload = arguments.Count == 1
            ? string.Empty
            : string.Join(' ', arguments.Skip(1));
        return new TmuxOutputEvent(arguments[0], OptionParser.DecodeEscapes(payload));
    }

    private void FailPending()
    {
        lock (_pending)
        {
            while (_pending.Count > 0)
            {
                _pending.Dequeue().Completion.TrySetException(new InvalidOperationException(
                    "The tmux control client exited before answering."));
            }
        }
    }
}
