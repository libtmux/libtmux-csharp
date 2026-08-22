namespace LibTmux.Examples.Snippets;

/// <summary>Compile-checked psmux examples published by the Windows preview guide.</summary>
public static class Psmux
{
    /// <summary>Reads the sole session, its windows, its panes, and pane text.</summary>
    [Example(
        "Query one pinned psmux namespace from Windows or WSL",
        RunsInDefaultSuite = false)]
    public static async Task QueryPsmux()
    {
        #region QueryPsmux
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken cancellationToken = cancellation.Token;

        string executable = Environment.GetEnvironmentVariable("LIBTMUX_PSMUX_BINARY")
            ?? throw new InvalidOperationException("LIBTMUX_PSMUX_BINARY is required.");
        string dataDirectory = Environment.GetEnvironmentVariable("PSMUX_DATA_DIR")
            ?? throw new InvalidOperationException("PSMUX_DATA_DIR is required.");
        string namespaceName = Environment.GetEnvironmentVariable("LIBTMUX_PSMUX_NAMESPACE")
            ?? throw new InvalidOperationException("LIBTMUX_PSMUX_NAMESPACE is required.");

        PsmuxServer server = await PsmuxServer.ConnectAsync(
            new PsmuxConnectionOptions(
                executablePath: executable,
                expectedBinarySha256: PsmuxServer.SupportedBinarySha256,
                dataDirectory: dataDirectory,
                namespaceName: namespaceName),
            cancellationToken);
        PsmuxSession session = await server.GetSessionAsync(cancellationToken);

        Console.WriteLine($"{session.Id} {session.Name}");
        foreach (PsmuxWindow window in await session.GetWindowsAsync(cancellationToken))
        {
            Console.WriteLine($"  {window.Id} {window.Index}: {window.Name}");
            foreach (PsmuxPane pane in await window.GetPanesAsync(cancellationToken))
            {
                IReadOnlyList<string> lines = await pane.CaptureAsync(
                    new PsmuxCaptureOptions(joinWrappedLines: true),
                    cancellationToken);
                Console.WriteLine($"    {pane.Id} {pane.Width}x{pane.Height}");
                foreach (string line in lines)
                {
                    Console.WriteLine($"      {line}");
                }
            }
        }
        #endregion
    }
}
