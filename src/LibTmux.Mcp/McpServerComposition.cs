using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            .WithTools<ReadTools>()
            .WithResources<HierarchyResources>()
            .WithPrompts<RecipePrompts>()
            .WithRequestFilters(filters => filters.AddCallToolFilter(ToolFailureFilter.Create()))

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
                        .UnsubscribeAsync(uri)
                        .ConfigureAwait(false);
                }

                return new EmptyResult();
            })

            // The revision that replaced resources/subscribe answers listen
            // itself, granting the subscription without telling the
            // application — so a client on a current revision would subscribe
            // and hear nothing. Owning the stream is what closes that.
            .WithSubscriptionsListenHandler(SubscriptionStream.Create())

            // The protocol's own answer to a call that waits. A client that
            // declares the extension gets a handle back at once and collects
            // later; one that has not keeps the blocking call it had.
            .WithTasks(
                new InMemoryMcpTaskStore(),
                tasks => tasks.ExecutionModeSelector = TaskCapableTools.Select);

        // Registration, not filtering. A tool the operator's tier does not
        // allow never reaches the model's list, so it cannot be called by name,
        // guessed at, or argued for.
        if (policy.Allows(SafetyTier.Mutating))
        {
            builder.WithTools<WriteTools>();
        }

        if (policy.Allows(SafetyTier.Destructive))
        {
            builder.WithTools<DestructiveTools>();
        }

        return builder;
    }
}
