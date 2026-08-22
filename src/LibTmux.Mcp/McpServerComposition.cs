using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <summary>Assembles the server: what it offers, and what it refuses to.</summary>
/// <remarks>
/// One description of the wiring, used by the executable and by the tests that
/// check what a client receives. Two descriptions would drift, and the one
/// that drifted would be the tested one.
/// </remarks>
[UnsupportedOSPlatform("windows")]
public static class McpServerComposition
{
    /// <summary>Registers everything the server needs and everything it offers.</summary>
    /// <param name="services">The container to register into.</param>
    /// <param name="policy">What the server will do.</param>
    /// <param name="connectionOptions">How to reach tmux.</param>
    /// <param name="callerPaneId">The pane the server runs in, when it runs in one.</param>
    /// <returns>The builder, for a transport to be chosen on.</returns>
    public static IMcpServerBuilder Add(
        IServiceCollection services,
        ServerPolicy policy,
        ServerConnectionOptions connectionOptions,
        string? callerPaneId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(connectionOptions);

        services.AddSingleton(policy);
        services.AddSingleton(provider => new TmuxConnectionAccessor(
            connectionOptions,
            connectionOptions.SocketName,
            provider.GetService<ILoggerFactory>()?.CreateLogger<TmuxConnectionAccessor>()));
        services.AddSingleton(provider => new PaneActivityHub(
            provider.GetService<ILoggerFactory>()?.CreateLogger<PaneActivityHub>()));
        services.AddSingleton(provider => new JobStore(
            provider.GetService<ILoggerFactory>()?.CreateLogger<JobStore>()));
        services.AddSingleton(provider => new HierarchyWatcher(
            provider.GetService<ILoggerFactory>()?.CreateLogger<HierarchyWatcher>()));
        services.AddSingleton<ReadTools>();
        services.AddSingleton<WriteTools>();
        services.AddSingleton<DestructiveTools>();
        services.AddSingleton<HierarchyResources>();
        var taskStore = new BoundedMcpTaskStore();
        services.AddSingleton(_ => new SubscriptionAdmission());

        IMcpServerBuilder builder = services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "tmux",
                    Version = LibTmuxMcp.Version,
                };
                options.ServerInstructions = ServerInstructions.Compose(policy, callerPaneId);
            })
            .WithTools<ReadTools>(ToolJson.Options)
            .WithResources<HierarchyResources>()
            .WithPrompts<RecipePrompts>()
            .WithRequestFilters(filters =>
            {
                filters.AddCallToolFilter(next =>
                    ToolResponseBudgetFilter.Create(policy)(ToolFailureFilter.Create()(next)));
                filters.AddReadResourceFilter(ResourceResponseBudgetFilter.Create(policy));
            })

            // A subscription is what turns the hierarchy from something a
            // client re-reads on a timer into something that tells it when to.
            .WithSubscribeToResourcesHandler(async (context, cancellationToken) =>
            {
                string? uri = context.Params?.Uri;
                if (uri is not null
                    && HierarchyWatcher.Watchable.Contains(uri)
                    && context.Services is IServiceProvider scope)
                {
                    McpServer notify = context.Server;
                    await scope.GetRequiredService<HierarchyWatcher>()
                        .SubscribeAsync(
                            uri,
                            notify,
                            async changed =>
                            {
                                foreach (string each in changed)
                                {
                                    await notify.SendNotificationAsync(
                                            "notifications/resources/updated",
                                            new { uri = each })
                                        .ConfigureAwait(false);
                                }
                            },
                            await scope.GetRequiredService<TmuxConnectionAccessor>()
                                .GetAsync(cancellationToken: cancellationToken)
                                .ConfigureAwait(false),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return new EmptyResult();
            })
            .WithUnsubscribeFromResourcesHandler(async (context, _) =>
            {
                if (context.Params?.Uri is string uri
                    && context.Services is IServiceProvider scope)
                {
                    await scope.GetRequiredService<HierarchyWatcher>()
                        .UnsubscribeAsync(uri, context.Server)
                        .ConfigureAwait(false);
                }

                return new EmptyResult();
            })

            // The current revision grants listen without notifying the application;
            // owning the stream is what starts the watcher that emits its events.
            .WithSubscriptionsListenHandler(SubscriptionStream.Create())

            // Task-capable clients may collect a wait later; other clients block.
            .WithTasks(
                taskStore,
                tasks => tasks.ExecutionModeSelector = TaskCapableTools.Select);

        services.AddSingleton<IConfigureOptions<McpServerOptions>>(
            new BoundedMcpTaskCancellationOptions(taskStore));

        // Registration, not filtering. A tool the operator's tier does not
        // allow never reaches the model's list, so it cannot be called by name,
        // guessed at, or argued for.
        if (policy.Allows(SafetyTier.Mutating))
        {
            builder.WithTools<WriteTools>(ToolJson.Options);
        }

        if (policy.Allows(SafetyTier.Destructive))
        {
            builder.WithTools<DestructiveTools>(ToolJson.Options);
        }

        return builder;
    }
}
