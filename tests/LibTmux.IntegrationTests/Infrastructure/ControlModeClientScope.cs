using System.Diagnostics;
using System.Globalization;

namespace LibTmux.IntegrationTests.Infrastructure;

internal sealed class ControlModeClientScope : IAsyncDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(5);
    private readonly RawTmuxTestContext context;
    private readonly Process process;
    private int disposed;

    private ControlModeClientScope(
        RawTmuxTestContext context,
        Process process,
        string clientName)
    {
        this.context = context;
        this.process = process;
        ClientName = clientName;
    }

    public string ClientName { get; }

    public int ProcessId => process.Id;

    public static async Task<ControlModeClientScope> StartAsync(
        RawTmuxTestContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        ProcessStartInfo startInfo = context.CreateStartInfo(
            ["-C", "attach-session", "-t", context.SessionName],
            redirectStandardInput: true);
        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The tmux control client did not start.");

        try
        {
            string clientName = await WaitForClientAsync(
                context,
                process,
                cancellationToken);
            return new ControlModeClientScope(context, process, clientName);
        }
        catch (Exception startFailure)
        {
            try
            {
                await StopAndReapAsync(process);
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

    public async Task WriteLineAsync(string command, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref disposed) != 0,
            this);
        ArgumentNullException.ThrowIfNull(command);
        await process.StandardInput.WriteLineAsync(command.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref disposed) != 0,
            this);
        return await process.StandardOutput.ReadLineAsync(cancellationToken);
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
                ["detach-client", "-t", ClientName],
                cleanup.Token);
            if (detached.ExitCode != 0)
            {
                failures.Add(new InvalidOperationException(
                    $"tmux failed to detach the control client: {detached.StandardErrorText}"));
            }

            await process.WaitForExitAsync(cleanup.Token);
        }
        catch (Exception detachFailure)
        {
            failures.Add(detachFailure);
        }

        try
        {
            await StopAndReapAsync(process);
        }
        catch (Exception cleanupFailure)
        {
            failures.Add(cleanupFailure);
        }

        process.StandardInput.Dispose();
        process.Dispose();
        if (failures.Count > 0)
        {
            throw new AggregateException(failures);
        }
    }

    private static async Task<string> WaitForClientAsync(
        RawTmuxTestContext context,
        Process process,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = new(StartupTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            timeout.Token,
            cancellationToken);
        string processId = process.Id.ToString(CultureInfo.InvariantCulture);
        try
        {
            while (true)
            {
                linked.Token.ThrowIfCancellationRequested();
                if (process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"The control client exited with code {process.ExitCode} before attaching.");
                }

                RawTmuxResult clients = await context.ExecuteAsync(
                    ["list-clients", "-F", "#{client_pid}\t#{client_name}\t#{client_control_mode}"],
                    linked.Token);
                foreach (string line in clients.StandardOutputLines)
                {
                    string[] fields = line.Split('\t');
                    if (fields.Length == 3
                        && fields[0] == processId
                        && fields[2] == "1")
                    {
                        return fields[1];
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10), linked.Token);
            }
        }
        catch (OperationCanceledException) when (
            timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("tmux did not expose the control client in time.");
        }
    }

    private static async Task StopAndReapAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: false);
        }

        using CancellationTokenSource cleanup = new(StartupTimeout);
        await process.WaitForExitAsync(cleanup.Token);
    }
}
