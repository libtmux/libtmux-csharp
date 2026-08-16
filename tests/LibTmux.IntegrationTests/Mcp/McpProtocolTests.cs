using System.IO.Pipelines;
using System.Runtime.Versioning;
using LibTmux.IntegrationTests.Transport;
using LibTmux.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LibTmux.IntegrationTests;

/// <summary>What a client actually receives over the wire.</summary>
/// <remarks>
/// The tool tests check what the tools do to tmux. These check the contract a
/// client reads before calling anything: the names, the annotations it gates
/// on, the schemas it validates against, and the guidance that decides whether
/// it routes a question here at all. None of that is visible from calling a
/// method directly.
/// </remarks>
[UnsupportedOSPlatform("windows")]
public sealed class McpProtocolTests
{
    [UnixFact]
    public async Task Reading_tools_are_annotated_so_a_client_does_not_prompt_for_a_listing()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using ProtocolHarness harness = await ProtocolHarness.StartAsync(token);

        IList<McpClientTool> tools = await harness.Client.ListToolsAsync(cancellationToken: token);
        McpClientTool listing = tools.Single(tool => tool.Name == "tmux_list_panes");

        Assert.True(listing.ProtocolTool.Annotations?.ReadOnlyHint);

        // A mutating tool has to say it is not destructive, because the spec
        // default for destructiveHint is true and a client that gates on it
        // would otherwise prompt before a split.
        McpClientTool split = tools.Single(tool => tool.Name == "tmux_split_pane");
        Assert.False(split.ProtocolTool.Annotations?.DestructiveHint ?? true);
    }

    [UnixFact]
    public async Task Every_tool_says_what_it_answers()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using ProtocolHarness harness = await ProtocolHarness.StartAsync(token);

        IList<McpClientTool> tools = await harness.Client.ListToolsAsync(cancellationToken: token);

        Assert.NotEmpty(tools);
        foreach (McpClientTool tool in tools)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(tool.ProtocolTool.Description),
                $"{tool.Name} has no description");

            // A schema is what lets a client destructure a result instead of
            // re-parsing prose out of it. tmux_display_message is exempt: it
            // answers whatever tmux expanded, which has no shape.
            if (tool.Name != "tmux_display_message")
            {
                Assert.True(
                    tool.ProtocolTool.OutputSchema.HasValue,
                    $"{tool.Name} advertises no output schema");
            }
        }
    }

    [UnixFact]
    public async Task A_result_arrives_as_structured_content()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using ProtocolHarness harness = await ProtocolHarness.StartAsync(token);

        // Deliberately against a socket with no tmux server behind it: that is
        // the first thing an assistant asks, and it must be an answer rather
        // than an error.
        CallToolResult listed = await harness.Client.CallToolAsync(
            "tmux_list_sessions",
            cancellationToken: token);

        Assert.NotEqual(true, listed.IsError);
        Assert.NotNull(listed.StructuredContent);
    }

    [UnixFact]
    public async Task The_destructive_tier_is_absent_unless_the_operator_asks_for_it()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using ProtocolHarness harness = await ProtocolHarness.StartAsync(
            token,
            SafetyTier.Mutating);

        IList<McpClientTool> tools = await harness.Client.ListToolsAsync(cancellationToken: token);
        IEnumerable<string> names = tools.Select(tool => tool.Name);

        // Not registered rather than refused: a tool that is not in the list
        // cannot be called by name, guessed at, or argued for.
        Assert.DoesNotContain("tmux_kill_session", names);
        Assert.DoesNotContain("tmux_kill_server", names);
        Assert.Contains("tmux_split_pane", names);
        Assert.Contains("tmux_list_panes", names);
    }

    [UnixFact]
    public async Task The_readonly_tier_offers_nothing_that_changes_tmux()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using ProtocolHarness harness = await ProtocolHarness.StartAsync(
            token,
            SafetyTier.ReadOnly);

        IList<McpClientTool> tools = await harness.Client.ListToolsAsync(cancellationToken: token);

        Assert.All(
            tools,
            tool => Assert.True(
                tool.ProtocolTool.Annotations?.ReadOnlyHint == true,
                $"{tool.Name} is offered at the readonly tier but is not annotated read-only"));
    }

    [UnixFact]
    public async Task The_hierarchy_is_readable_as_a_resource()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using ProtocolHarness harness = await ProtocolHarness.StartAsync(token);

        await harness.Client.CallToolAsync(
            "tmux_create_session",
            new Dictionary<string, object?> { ["name"] = "probe" },
            cancellationToken: token);

        IList<McpClientResource> resources = await harness.Client.ListResourcesAsync(
            cancellationToken: token);
        Assert.Contains(resources, resource => resource.Uri == "tmux://hierarchy");

        ReadResourceResult read = await harness.Client.ReadResourceAsync(
            "tmux://hierarchy",
            cancellationToken: token);
        Assert.NotEmpty(read.Contents);
    }

    [UnixFact]
    public async Task The_recipes_are_offered_as_prompts()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using ProtocolHarness harness = await ProtocolHarness.StartAsync(token);

        IList<McpClientPrompt> prompts = await harness.Client.ListPromptsAsync(
            cancellationToken: token);

        Assert.Contains(prompts, prompt => prompt.Name == "tmux_run_and_report");
        Assert.Contains(prompts, prompt => prompt.Name == "tmux_diagnose_pane");
    }

    [UnixFact]
    public async Task A_failure_arrives_as_an_error_result_rather_than_a_dropped_connection()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using ProtocolHarness harness = await ProtocolHarness.StartAsync(token);

        // A session has to exist first, or the failure under test would be
        // "no server running" rather than "no such pane".
        await harness.Client.CallToolAsync(
            "tmux_create_session",
            new Dictionary<string, object?> { ["name"] = "probe" },
            cancellationToken: token);

        CallToolResult failed = await harness.Client.CallToolAsync(
            "tmux_capture_pane",
            new Dictionary<string, object?> { ["paneId"] = "%999" },
            cancellationToken: token);

        Assert.True(failed.IsError);
        string text = Assert.IsType<TextContentBlock>(failed.Content[0]).Text;

        // The message has to name what to do next. "An error occurred" costs a
        // model a turn and teaches it nothing.
        Assert.Contains("%999", text, StringComparison.Ordinal);
        Assert.Contains("tmux_list_panes", text, StringComparison.Ordinal);
    }

    /// <summary>A server and a client joined by a pipe, over a throwaway socket.</summary>
    /// <remarks>
    /// Composed through <see cref="McpServerComposition" /> rather than wired
    /// by hand, so what these tests check is what the executable actually
    /// serves.
    /// </remarks>
    private sealed class ProtocolHarness : IAsyncDisposable
    {
        private readonly McpServer _server;
        private readonly ServiceProvider _services;

        private ProtocolHarness(McpServer server, McpClient client, ServiceProvider services)
        {
            _server = server;
            Client = client;
            _services = services;
        }

        internal McpClient Client { get; }

        internal static async Task<ProtocolHarness> StartAsync(
            CancellationToken cancellationToken,
            SafetyTier tier = SafetyTier.Destructive)
        {
            ServiceCollection services = new();
            services.AddLogging();
            McpServerComposition.Add(
                services,
                new ServerPolicy { Tier = tier, WaitCeiling = TimeSpan.FromSeconds(10) },
                new ServerConnectionOptions(
                    tmuxBinaryPath: System.Environment.GetEnvironmentVariable("LIBTMUX_TMUX") ?? "tmux",
                    socketName: $"ltp-{Guid.NewGuid():N}"[..20],
                    configurationFile: "/dev/null"),
                callerPaneId: null);
            ServiceProvider provider = services.BuildServiceProvider();

            Pipe clientToServer = new();
            Pipe serverToClient = new();
            McpServer server = McpServer.Create(
                new StreamServerTransport(
                    clientToServer.Reader.AsStream(),
                    serverToClient.Writer.AsStream()),
                provider.GetRequiredService<IOptions<McpServerOptions>>().Value,
                provider.GetRequiredService<ILoggerFactory>(),
                provider);
            _ = server.RunAsync(CancellationToken.None);

            McpClient client = await McpClient.CreateAsync(
                new StreamClientTransport(
                    clientToServer.Writer.AsStream(),
                    serverToClient.Reader.AsStream()),
                cancellationToken: cancellationToken);

            return new ProtocolHarness(server, client, provider);
        }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync().ConfigureAwait(false);
            await _server.DisposeAsync().ConfigureAwait(false);
            await _services.DisposeAsync().ConfigureAwait(false);
        }
    }
}
