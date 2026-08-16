using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <summary>Owns a client's long-lived subscription stream.</summary>
/// <remarks>
/// <para>
/// The 2026-07-28 revision replaced <c>resources/subscribe</c> with
/// <c>subscriptions/listen</c>: one long-lived request whose held-open
/// response carries the events. The SDK answers it out of the box, but its
/// built-in handling grants the resource subscriptions without telling the
/// application, so nothing here would ever learn to start watching and a
/// client on a current revision would subscribe successfully and hear
/// nothing.
/// </para>
/// <para>
/// Taking the stream over means owning its whole contract: exactly one
/// acknowledgement before any event, every event tagged with the listen
/// request's id so a client sharing one channel can tell streams apart, and
/// staying up until the request is cancelled.
/// </para>
/// </remarks>
[UnsupportedOSPlatform("windows")]
internal static class SubscriptionStream
{
    /// <summary>Builds the handler that owns <c>subscriptions/listen</c>.</summary>
    /// <returns>The handler.</returns>
    internal static McpRequestHandler<SubscriptionsListenRequestParams, EmptyResult> Create() =>
        async (request, cancellationToken) =>
    {
        SubscriptionsListenNotifications requested = request.Params?.Notifications
            ?? new SubscriptionsListenNotifications();

        // Only what this server can actually deliver is granted. Claiming a
        // subscription and never sending it is worse than declining it: the
        // client waits instead of falling back to reading.
        List<string> watched = [.. (requested.ResourceSubscriptions ?? [])
            .Where(HierarchyWatcher.Watchable.Contains)];

        SubscriptionsListenNotifications granted = new()
        {
            ToolsListChanged = requested.ToolsListChanged,
            PromptsListChanged = requested.PromptsListChanged,
            ResourcesListChanged = requested.ResourcesListChanged,
            ResourceSubscriptions = watched.Count > 0 ? watched : null,
        };

        string subscriptionId = request.JsonRpcRequest.Id.ToString();
        McpServer server = request.Server;

        // The acknowledgement goes first, before any event, and says what was
        // granted rather than what was asked for.
        await SendAsync(
                server,
                NotificationMethods.SubscriptionsAcknowledgedNotification,
                new JsonObject
                {
                    ["notifications"] = Describe(granted),
                },
                subscriptionId,
                cancellationToken)
            .ConfigureAwait(false);

        HierarchyWatcher? watcher = watched.Count > 0
            ? request.Services?.GetService<HierarchyWatcher>()
            : null;

        if (watcher is not null && request.Services is IServiceProvider scope)
        {
            Server tmux = await scope.GetRequiredService<TmuxConnectionAccessor>()
                .GetAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            foreach (string uri in watched)
            {
                await watcher.SubscribeAsync(
                        uri,
                        async changed =>
                        {
                            foreach (string each in changed)
                            {
                                await SendAsync(
                                        server,
                                        NotificationMethods.ResourceUpdatedNotification,
                                        new JsonObject { ["uri"] = each },
                                        subscriptionId,
                                        CancellationToken.None)
                                    .ConfigureAwait(false);
                            }
                        },
                        tmux,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        try
        {
            // The response is the stream. Returning early would close it and
            // take the subscription with it, so this waits out the request.
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The client stopped listening, which is how a listen ends.
        }
        finally
        {
            if (watcher is not null)
            {
                foreach (string uri in watched)
                {
                    await watcher.UnsubscribeAsync(uri).ConfigureAwait(false);
                }
            }
        }

        return new EmptyResult();
    };

    /// <summary>Describes what was granted, omitting what was not.</summary>
    private static JsonObject Describe(SubscriptionsListenNotifications granted)
    {
        JsonObject described = [];
        if (granted.ToolsListChanged == true)
        {
            described["toolsListChanged"] = true;
        }

        if (granted.PromptsListChanged == true)
        {
            described["promptsListChanged"] = true;
        }

        if (granted.ResourcesListChanged == true)
        {
            described["resourcesListChanged"] = true;
        }

        if (granted.ResourceSubscriptions is { Count: > 0 } subscriptions)
        {
            described["resourceSubscriptions"] = new JsonArray(
                [.. subscriptions.Select(uri => (JsonNode)JsonValue.Create(uri)!)]);
        }

        return described;
    }

    /// <summary>Sends one event tagged with the stream it belongs to.</summary>
    /// <remarks>
    /// The tag is what lets a client on one channel — stdio shares a single
    /// one — tell this stream's events from another's.
    /// </remarks>
    private static async Task SendAsync(
        McpServer server,
        string method,
        JsonObject parameters,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        parameters["_meta"] = new JsonObject
        {
            [MetaKeys.SubscriptionId] = subscriptionId,
        };

        try
        {
            await server.SendNotificationAsync(method, parameters, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException
            or InvalidOperationException or OperationCanceledException)
        {
            // The client hung up mid-stream. The subscription dies with it.
        }
    }
}
