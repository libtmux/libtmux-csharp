using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace LibTmux.IntegrationTests.Infrastructure;

internal sealed class PtyAttachedClientScope : IAsyncDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(5);
    private const int OutputCapacity = 64 * 1024;
    private readonly RawTmuxTestContext context;
    private readonly BoundedCaptureStream outputCapture;
    private readonly Process process;
    private readonly Task standardErrorPump;
    private readonly Task standardOutputPump;
    private int disposed;

    private PtyAttachedClientScope(
        RawTmuxTestContext context,
        Process process,
        BoundedCaptureStream outputCapture,
        Task standardOutputPump,
        Task standardErrorPump,
        int clientProcessId,
        string tty)
    {
        this.context = context;
        this.process = process;
        this.outputCapture = outputCapture;
        this.standardOutputPump = standardOutputPump;
        this.standardErrorPump = standardErrorPump;
        ClientProcessId = clientProcessId;
        Tty = tty;
    }

    public int ClientProcessId { get; }

    public string Tty { get; }

    public static async Task<PtyAttachedClientScope> StartAsync(
        RawTmuxTestContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        ProcessStartInfo startInfo = CreateStartInfo(context);
        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The PTY launcher did not start.");
        BoundedCaptureStream outputCapture = new(OutputCapacity);
        Task stdoutPump = process.StandardOutput.BaseStream.CopyToAsync(
            outputCapture,
            CancellationToken.None);
        Task stderrPump = process.StandardError.BaseStream.CopyToAsync(
            Stream.Null,
            CancellationToken.None);

        try
        {
            (int clientProcessId, string tty) = await WaitForClientAsync(
                context,
                process,
                cancellationToken);
            return new PtyAttachedClientScope(
                context,
                process,
                outputCapture,
                stdoutPump,
                stderrPump,
                clientProcessId,
                tty);
        }
        catch (Exception startFailure)
        {
            try
            {
                await StopAndSettleAsync(process, stdoutPump, stderrPump);
            }
            catch (Exception cleanupFailure)
            {
                process.Dispose();
                throw new AggregateException(startFailure, cleanupFailure);
            }

            process.Dispose();
            throw;
        }
    }

    public byte[] ReadOutputSnapshot()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref disposed) != 0,
            this);
        return outputCapture.Snapshot();
    }

    public async Task WriteAsync(
        ReadOnlyMemory<byte> input,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref disposed) != 0,
            this);
        cancellationToken.ThrowIfCancellationRequested();
        await process.StandardInput.BaseStream.WriteAsync(input, cancellationToken);
        await process.StandardInput.BaseStream.FlushAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        List<Exception> failures = [];
        try
        {
            using CancellationTokenSource cleanup = new(StartupTimeout);
            RawTmuxResult detached = await context.ExecuteAsync(
                ["detach-client", "-t", Tty],
                cleanup.Token);

            // Detaching is a state this wants to reach, not a command that has
            // to succeed. A test that detached the client itself, or a server
            // that has already gone, leaves nothing to detach and tmux says so
            // with an error; only a client still attached is a real failure.
            if (detached.ExitCode != 0 && await IsAttachedAsync(cleanup.Token))
            {
                failures.Add(new InvalidOperationException(
                    $"tmux failed to detach the PTY client: {detached.StandardErrorText}"));
            }

            // Giving the client a chance to leave on its own keeps the common
            // path quiet, but a suspended one never takes it, so the wait is
            // an optimisation and the kill below is the guarantee.
            try
            {
                await process.WaitForExitAsync(cleanup.Token);
            }
            catch (OperationCanceledException) when (cleanup.IsCancellationRequested)
            {
            }
        }
        catch (Exception detachFailure)
        {
            failures.Add(detachFailure);
        }

        try
        {
            await StopAndSettleAsync(process, standardOutputPump, standardErrorPump);
        }
        catch (Exception cleanupFailure)
        {
            failures.Add(cleanupFailure);
        }

        process.Dispose();
        if (failures.Count > 0)
        {
            throw new AggregateException(failures);
        }
    }

    private async Task<bool> IsAttachedAsync(CancellationToken cancellationToken)
    {
        RawTmuxResult clients = await context.ExecuteAsync(
            ["list-clients", "-F", "#{client_tty}"],
            cancellationToken);
        return clients.ExitCode == 0
            && clients.StandardOutputLines.Any(
                line => string.Equals(line, Tty, StringComparison.Ordinal));
    }

    private static ProcessStartInfo CreateStartInfo(RawTmuxTestContext context)
    {
        IReadOnlyList<string> tmuxArguments = context.BuildInvocationArguments(
            ["attach-session", "-t", context.SessionName]);
        ProcessStartInfo startInfo;
        if (OperatingSystem.IsLinux())
        {
            startInfo = new ProcessStartInfo("/usr/bin/script")
            {
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("-q");
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(BuildShellCommand(context.TmuxBinaryPath, tmuxArguments));
            startInfo.ArgumentList.Add("/dev/null");
        }
        else if (OperatingSystem.IsMacOS())
        {
            startInfo = new ProcessStartInfo("/usr/bin/script")
            {
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("-q");
            startInfo.ArgumentList.Add("/dev/null");
            startInfo.ArgumentList.Add(context.TmuxBinaryPath);
            foreach (string argument in tmuxArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
        }
        else
        {
            throw new PlatformNotSupportedException(
                "The PTY test client requires Linux or macOS.");
        }

        RawTmuxTestContext.ConfigureEnvironment(startInfo);
        return startInfo;
    }

    private static async Task<(int ClientProcessId, string Tty)> WaitForClientAsync(
        RawTmuxTestContext context,
        Process launcher,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = new(StartupTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            timeout.Token,
            cancellationToken);
        try
        {
            while (true)
            {
                linked.Token.ThrowIfCancellationRequested();
                if (launcher.HasExited)
                {
                    throw new InvalidOperationException(
                        $"The PTY launcher exited with code {launcher.ExitCode} before tmux attached.");
                }

                RawTmuxResult clients = await context.ExecuteAsync(
                    ["list-clients", "-F", "#{client_pid}\t#{client_tty}\t#{client_control_mode}"],
                    linked.Token);
                foreach (string line in clients.StandardOutputLines)
                {
                    string[] fields = line.Split('\t');
                    if (fields.Length == 3
                        && fields[2] == "0"
                        && int.TryParse(
                            fields[0],
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out int processId)
                        && !string.IsNullOrEmpty(fields[1]))
                    {
                        return (processId, fields[1]);
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10), linked.Token);
            }
        }
        catch (OperationCanceledException) when (
            timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("tmux did not expose the PTY-attached client in time.");
        }
    }

    private static string BuildShellCommand(
        string executable,
        IReadOnlyList<string> arguments)
    {
        StringBuilder command = new(ShellQuote(executable));
        foreach (string argument in arguments)
        {
            command.Append(' ');
            command.Append(ShellQuote(argument));
        }

        return command.ToString();
    }

    private static string ShellQuote(string value) =>
        $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";

    private static async Task StopAndSettleAsync(
        Process process,
        Task standardOutputPump,
        Task standardErrorPump)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }

        using (CancellationTokenSource reap = new(StartupTimeout))
        {
            await process.WaitForExitAsync(reap.Token);
        }

        process.StandardInput.Dispose();
        using CancellationTokenSource settle = new(StartupTimeout);
        await Task.WhenAll(standardOutputPump, standardErrorPump).WaitAsync(settle.Token);
    }

    private sealed class BoundedCaptureStream(int capacity) : Stream
    {
        private readonly byte[] buffer = new byte[capacity];
        private readonly object sync = new();
        private int count;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length
        {
            get
            {
                lock (sync)
                {
                    return count;
                }
            }
        }

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public byte[] Snapshot()
        {
            lock (sync)
            {
                return buffer[..count];
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] target, int offset, int targetCount) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] source, int offset, int sourceCount) =>
            Write(source.AsSpan(offset, sourceCount));

        public override void Write(ReadOnlySpan<byte> source)
        {
            lock (sync)
            {
                if (source.Length >= buffer.Length)
                {
                    source[^buffer.Length..].CopyTo(buffer);
                    count = buffer.Length;
                    return;
                }

                int overflow = Math.Max(0, count + source.Length - buffer.Length);
                if (overflow > 0)
                {
                    Buffer.BlockCopy(buffer, overflow, buffer, 0, count - overflow);
                    count -= overflow;
                }

                source.CopyTo(buffer.AsSpan(count));
                count += source.Length;
            }
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> source,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(source.Span);
            return ValueTask.CompletedTask;
        }
    }
}
