using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibTmux.Mcp;

/// <summary>Serves tmux over the Model Context Protocol.</summary>
/// <remarks>
/// The protocol speaks over standard output, so every log line has to go to
/// standard error. A message written to the wrong stream is not a stray line
/// in a log: it corrupts the protocol and the client disconnects.
/// </remarks>
[UnsupportedOSPlatform("windows")]
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (OperatingSystem.IsWindows())
        {
            await Console.Error.WriteLineAsync("tmux does not run on Windows.").ConfigureAwait(false);
            return 1;
        }

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddConsole(options =>
            options.LogToStandardErrorThreshold = LogLevel.Trace);

        // A socket named on the command line lets one assistant drive a server
        // that is not the ambient one, which is what a test or a sandbox wants.
        string? socket = args.Length > 0 ? args[0] : null;
        builder.Services.AddSingleton(
            new TmuxConnectionAccessor(
                socket is null
                    ? null
                    : new ServerConnectionOptions(socketName: socket)));

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<TmuxTools>();

        await builder.Build().RunAsync().ConfigureAwait(false);
        return 0;
    }
}
