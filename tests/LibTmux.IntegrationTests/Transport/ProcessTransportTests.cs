using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

using LibTmux.IntegrationTests.Infrastructure;
using LibTmux.IntegrationTests.Parity;
using LibTmux.Internal;

namespace LibTmux.IntegrationTests.Transport;

internal static class UnixTestEnvironment
{
    public static bool IsUnix => !OperatingSystem.IsWindows();
}

internal sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute(
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = "Requires a Unix process environment.";
        SkipType = typeof(UnixTestEnvironment);
        SkipUnless = nameof(UnixTestEnvironment.IsUnix);
    }
}

[UnsupportedOSPlatform("windows")]
public sealed class ProcessTransportTests
{
    [UnixFact]
    public async Task Test_child_preserves_concurrent_raw_stdout_and_stderr()
    {
        byte[] stdoutChunk = [0x00, 0x6f, 0x0a, 0xff];
        byte[] stderrChunk = [0x65, 0x00, 0x0d, 0x80];
        const int RepeatCount = 32 * 1024;

        TmuxProcessTransport transport = CreateTransport();

        TmuxCommandResult result = await transport.ExecuteAsync(
            [
                "concurrent-raw",
                Convert.ToBase64String(stdoutChunk),
                Convert.ToBase64String(stderrChunk),
                RepeatCount.ToString(CultureInfo.InvariantCulture),
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(RepeatChunk(stdoutChunk, RepeatCount), result.StandardOutput.ToArray());
        Assert.Equal(RepeatChunk(stderrChunk, RepeatCount), result.StandardError.ToArray());
    }

    [UnixFact]
    public async Task Test_child_preserves_invalid_bytes()
    {
        TmuxProcessTransport transport = CreateTransport();

        TmuxCommandResult result = await transport.ExecuteAsync(
            ["invalid-utf8"],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new byte[] { 0x66, 0x80, 0xff, 0x0a }, result.StandardOutput.ToArray());
        Assert.Empty(result.StandardError.ToArray());
        Assert.Equal(["f\\x80\\xff"], result.StandardOutputLines);
    }

    [UnixFact]
    public async Task Test_child_projects_partial_final_output()
    {
        TmuxProcessTransport transport = CreateTransport();

        TmuxCommandResult result = await transport.ExecuteAsync(
            ["partial-final"],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("final-record"u8.ToArray(), result.StandardOutput.ToArray());
        Assert.Equal("final-error"u8.ToArray(), result.StandardError.ToArray());
        Assert.Equal(["final-record"], result.StandardOutputLines);
        Assert.Equal(["final-error"], result.StandardErrorLines);
    }

    [UnixFact]
    public async Task Test_child_returns_nonzero_exit()
    {
        TmuxProcessTransport transport = CreateTransport();

        TmuxCommandResult result = await transport.ExecuteAsync(
            ["nonzero-exit", "23"],
            TestContext.Current.CancellationToken);

        Assert.Equal(23, result.ExitCode);
        Assert.Equal("nonzero-output\n"u8.ToArray(), result.StandardOutput.ToArray());
        Assert.Equal("nonzero-error\n"u8.ToArray(), result.StandardError.ToArray());
    }

    [UnixFact]
    public async Task Test_child_bounds_a_held_pump()
    {
        string readyPath = Path.Combine(
            Path.GetTempPath(),
            $"libtmux-pump-{Guid.NewGuid():N}.ready");
        TaskCompletionSource<int> started = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TmuxProcessTransport transport = CreateTransport(startInfo =>
            StartProcess(startInfo, started));
        using CancellationTokenSource cancellation = new();
        Task<TmuxCommandResult> execution = transport.ExecuteAsync(
            ["hold-pump", readyPath],
            cancellation.Token);
        int? clientProcessId = null;

        try
        {
            clientProcessId = await started.Task.WaitAsync(
                TestContext.Current.CancellationToken);
            using CancellationTokenSource readiness = CancellationTokenSource
                .CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            readiness.CancelAfter(TimeSpan.FromSeconds(2));
            Assert.Equal("pump-ready", await WaitForFileAsync(readyPath, readiness.Token));

            cancellation.Cancel();
            TmuxOperationCanceledException error =
                await Assert.ThrowsAsync<TmuxOperationCanceledException>(() => execution);

            Assert.Equal(clientProcessId.Value, error.ClientProcessId);
            Assert.False(IsProcessAlive(clientProcessId.Value));
        }
        finally
        {
            cancellation.Cancel();
            await IgnoreFailureAsync(execution);
            if (clientProcessId is not null)
            {
                await KillIfRunningAsync(clientProcessId.Value);
            }

            File.Delete(readyPath);
        }
    }

    [UnixFact]
    public async Task Post_start_cancellation_reaps_client_but_leaves_descendant_alive()
    {
        string pidPath = Path.Combine(
            Path.GetTempPath(),
            $"libtmux-child-{Guid.NewGuid():N}.pid");
        TaskCompletionSource<int> started = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TmuxProcessTransport transport = CreateTransport(startInfo =>
            StartProcess(startInfo, started));
        using CancellationTokenSource cancellation = new();
        Task<TmuxCommandResult> execution = transport.ExecuteAsync(
            ["descendant-survival", pidPath],
            cancellation.Token);
        int? clientProcessId = null;
        int? descendantProcessId = null;

        try
        {
            clientProcessId = await started.Task.WaitAsync(
                TestContext.Current.CancellationToken);
            using CancellationTokenSource readiness = CancellationTokenSource
                .CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            readiness.CancelAfter(TimeSpan.FromSeconds(2));
            descendantProcessId = int.Parse(
                await WaitForFileAsync(pidPath, readiness.Token),
                CultureInfo.InvariantCulture);

            cancellation.Cancel();
            TmuxOperationCanceledException error =
                await Assert.ThrowsAsync<TmuxOperationCanceledException>(() => execution);

            Assert.Equal(clientProcessId.Value, error.ClientProcessId);
            Assert.False(IsProcessAlive(clientProcessId.Value));
            using Process descendant = Process.GetProcessById(descendantProcessId.Value);
            Assert.False(descendant.HasExited);
        }
        finally
        {
            if (descendantProcessId is null && clientProcessId is not null)
            {
                descendantProcessId = await TryReadProcessIdAsync(pidPath);
            }

            cancellation.Cancel();
            await IgnoreFailureAsync(execution);

            if (clientProcessId is not null)
            {
                await KillIfRunningAsync(clientProcessId.Value);
            }

            if (descendantProcessId is not null)
            {
                await KillIfRunningAsync(descendantProcessId.Value);
            }

            File.Delete(pidPath);
        }
    }

    [UnixFact]
    public async Task Test_child_reports_cleanup_faults()
    {
        string readyPath = Path.Combine(
            Path.GetTempPath(),
            $"libtmux-cleanup-{Guid.NewGuid():N}.ready");
        CleanupFailingLauncher launcher = new();
        TmuxProcessTransport transport = new(
            "dotnet",
            launcher,
            [TestChildAssemblyPath()]);
        using CancellationTokenSource cancellation = new();
        Task<TmuxCommandResult> execution = transport.ExecuteAsync(
            ["cleanup-fault", readyPath],
            cancellation.Token);
        int? clientProcessId = null;

        try
        {
            clientProcessId = await launcher.Started.Task.WaitAsync(
                TestContext.Current.CancellationToken);
            using CancellationTokenSource readiness = CancellationTokenSource
                .CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            readiness.CancelAfter(TimeSpan.FromSeconds(2));
            Assert.Equal("cleanup-fault-ready", await WaitForFileAsync(readyPath, readiness.Token));

            cancellation.Cancel();
            TmuxCleanupException error =
                await Assert.ThrowsAsync<TmuxCleanupException>(() => execution);

            Assert.Equal(clientProcessId.Value, error.ClientProcessId);
            Assert.Equal(cancellation.Token, error.OriginalCancellation.CancellationToken);
            Assert.IsType<IOException>(error.CleanupFailure);
            Assert.False(IsProcessAlive(clientProcessId.Value));
        }
        finally
        {
            cancellation.Cancel();
            await IgnoreFailureAsync(execution);
            if (clientProcessId is not null)
            {
                await KillIfRunningAsync(clientProcessId.Value);
            }

            File.Delete(readyPath);
        }
    }

    [UnixFact]
    public async Task Pty_attached_client_scope_uses_real_pty()
    {
        await using RawTmuxTestContext context = await RawTmuxTestContext.StartAsync(
            TestContext.Current.CancellationToken);
        PtyAttachedClientScope client = await PtyAttachedClientScope.StartAsync(
            context,
            TestContext.Current.CancellationToken);
        string tty = client.Tty;

        try
        {
            RawTmuxResult attached = await context.ExecuteAsync(
                ["list-clients", "-F", "#{client_tty}:#{client_control_mode}"],
                TestContext.Current.CancellationToken);

            Assert.Equal(0, attached.ExitCode);
            Assert.Contains($"{tty}:0", attached.StandardOutputLines);
        }
        finally
        {
            await client.DisposeAsync();
        }

        RawTmuxResult detached = await context.ExecuteAsync(
            ["list-clients", "-F", "#{client_tty}"],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, detached.ExitCode);
        Assert.DoesNotContain(tty, detached.StandardOutputLines);
    }

    private static TmuxProcessTransport CreateTransport(
        Func<ProcessStartInfo, Process>? launcher = null) =>
        new("dotnet", [TestChildAssemblyPath()], launcher: launcher);

    private static Process StartProcess(
        ProcessStartInfo startInfo,
        TaskCompletionSource<int> started)
    {
        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The deterministic test child did not start.");
        started.TrySetResult(process.Id);
        return process;
    }

    private static string TestChildAssemblyPath()
    {
        DirectoryInfo frameworkDirectory = new(AppContext.BaseDirectory);
        DirectoryInfo configurationDirectory = frameworkDirectory.Parent
            ?? throw new InvalidOperationException("The test output has no configuration directory.");
        DirectoryInfo testsDirectory = configurationDirectory.Parent?.Parent?.Parent
            ?? throw new InvalidOperationException("The test output is outside the tests directory.");
        string path = Path.Combine(
            testsDirectory.FullName,
            "LibTmux.TestChild",
            "bin",
            configurationDirectory.Name,
            frameworkDirectory.Name,
            "LibTmux.TestChild.dll");

        return File.Exists(path)
            ? path
            : throw new FileNotFoundException("The deterministic test child was not built.", path);
    }

    private static byte[] RepeatChunk(byte[] chunk, int repeatCount)
    {
        byte[] expected = new byte[checked(chunk.Length * repeatCount)];
        for (int index = 0; index < repeatCount; index++)
        {
            chunk.CopyTo(expected, index * chunk.Length);
        }

        return expected;
    }

    private static async Task KillIfRunningAsync(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: false);
                using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(cleanup.Token);
            }
        }
        catch (ArgumentException)
        {
        }
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task<string> WaitForFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await File.ReadAllTextAsync(path, cancellationToken);
            }
            catch (FileNotFoundException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
            }
        }
    }

    private static async Task<int?> TryReadProcessIdAsync(string path)
    {
        using CancellationTokenSource recovery = new(TimeSpan.FromSeconds(2));
        try
        {
            string value = await WaitForFileAsync(path, recovery.Token);
            return int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int processId)
                && processId > 0
                ? processId
                : null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task IgnoreFailureAsync(Task task)
    {
        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(7));
        }
        catch (Exception)
        {
        }
    }

    private sealed class CleanupFailingLauncher : ITmuxProcessLauncher
    {
        public TaskCompletionSource<int> Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public ITmuxProcessHandle Start(ProcessStartInfo startInfo)
        {
            Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The cleanup-fault child did not start.");
            Started.TrySetResult(process.Id);
            return new CleanupFailingHandle(process);
        }
    }

    private sealed class CleanupFailingHandle(Process process) : ITmuxProcessHandle
    {
        public int Id => process.Id;

        public Stream StandardOutput => process.StandardOutput.BaseStream;

        public Stream StandardError => process.StandardError.BaseStream;

        public int ExitCode => process.ExitCode;

        public bool HasExited => process.HasExited;

        public void Kill()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: false);
            }

            throw new IOException("Injected cleanup fault.");
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken = default) =>
            process.WaitForExitAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class ProcessTransportPlatformContractTests
{
    [Fact]
    public void Process_transport_tests_have_runtime_Unix_skip_metadata()
    {
        AssertUnixProcessTestsHaveRuntimeSkip(typeof(ProcessTransportTests));
        AssertUnixProcessTestsHaveRuntimeSkip(typeof(Component01ParityTests));
    }

    private static void AssertUnixProcessTestsHaveRuntimeSkip(Type testType)
    {
        PropertyInfo? condition = typeof(UnixTestEnvironment).GetProperty(
            nameof(UnixTestEnvironment.IsUnix),
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(condition);
        Assert.Equal(typeof(bool), condition.PropertyType);
        foreach (MethodInfo method in testType.GetMethods())
        {
            FactAttribute? fact = method.GetCustomAttribute<FactAttribute>();
            if (fact is null)
            {
                continue;
            }

            Assert.Equal("Requires a Unix process environment.", fact.Skip);
            Assert.Equal(typeof(UnixTestEnvironment), fact.SkipType);
            Assert.Equal(nameof(UnixTestEnvironment.IsUnix), fact.SkipUnless);
        }
    }
}
