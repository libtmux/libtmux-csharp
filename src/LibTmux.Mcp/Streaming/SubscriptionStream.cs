using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
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
    internal const int SubscriptionIdMaxEncodedBytes = 256;

    /// <summary>Builds the handler that owns <c>subscriptions/listen</c>.</summary>
    /// <returns>The handler.</returns>
    internal static McpRequestHandler<SubscriptionsListenRequestParams, EmptyResult> Create() =>
        async (request, cancellationToken) =>
    {
        SubscriptionsListenNotifications requested = request.Params?.Notifications
            ?? new SubscriptionsListenNotifications();

        IReadOnlyList<string> watched = Canonicalize(requested.ResourceSubscriptions);
        if (request.Services is not IServiceProvider services)
        {
            throw new InvalidOperationException("Subscription services are unavailable.");
        }

        // Admission precedes the subscriber key and delivery callback. A
        // rejected stream therefore cannot leave detached watcher state.
        using SubscriptionAdmission.Lease admission = services
            .GetRequiredService<SubscriptionAdmission>()
            .Acquire(cancellationToken);

        return await ListenAsync(request, requested, watched, services, cancellationToken)
            .ConfigureAwait(false);
    };

    /// <summary>Returns the distinct resource subscriptions this server can deliver.</summary>
    internal static IReadOnlyList<string> Canonicalize(IEnumerable<string>? requested)
    {
        if (requested is null)
        {
            return [];
        }

        bool[] found = new bool[HierarchyWatcher.Watchable.Count];
        int remaining = found.Length;
        foreach (string candidate in requested)
        {
            for (int index = 0; index < HierarchyWatcher.Watchable.Count; index++)
            {
                if (found[index]
                    || !string.Equals(
                        candidate,
                        HierarchyWatcher.Watchable[index],
                        StringComparison.Ordinal))
                {
                    continue;
                }

                found[index] = true;
                remaining--;
                break;
            }

            if (remaining == 0)
            {
                break;
            }
        }

        List<string> canonical = new(found.Length);
        for (int index = 0; index < found.Length; index++)
        {
            if (found[index])
            {
                canonical.Add(HierarchyWatcher.Watchable[index]);
            }
        }

        return canonical;
    }

    /// <summary>Rejects a stream identifier too large to echo on every event.</summary>
    internal static RequestId ValidateSubscriptionId(RequestId subscriptionId)
    {
        if (subscriptionId.Id is not (string or long))
        {
            throw new McpException("The subscription request requires a JSON-RPC id.");
        }

        if (subscriptionId.Id is string text
            && (text.Length > SubscriptionIdMaxEncodedBytes
                || JsonEncodedText.Encode(text).EncodedUtf8Bytes.Length
                > SubscriptionIdMaxEncodedBytes))
        {
            throw new McpException(
                $"The subscription request id exceeds {SubscriptionIdMaxEncodedBytes} "
                + "JSON-encoded bytes. Use a shorter JSON-RPC id.");
        }

        return subscriptionId;
    }

    private static async Task<EmptyResult> ListenAsync(
        RequestContext<SubscriptionsListenRequestParams> request,
        SubscriptionsListenNotifications requested,
        IReadOnlyList<string> watched,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        // Only what this server can actually deliver is granted. Claiming a
        // subscription and never sending it is worse than declining it: the
        // client waits instead of falling back to reading.

        SubscriptionsListenNotifications granted = new()
        {
            ToolsListChanged = requested.ToolsListChanged,
            PromptsListChanged = requested.PromptsListChanged,
            ResourcesListChanged = requested.ResourcesListChanged,
            ResourceSubscriptions = watched.Count > 0 ? [.. watched] : null,
        };

        RequestId subscriptionId = ValidateSubscriptionId(request.JsonRpcRequest.Id);
        McpServer server = request.Server;
        HierarchyWatcher? watcher = watched.Count > 0
            ? services.GetService<HierarchyWatcher>()
            : null;
        object? subscriberKey = null;
        List<string> subscribed = [];
        TaskCompletionSource deliveryEnabled = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            if (watcher is not null)
            {
                Server tmux = await services.GetRequiredService<TmuxConnectionAccessor>()
                    .GetAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                subscriberKey = new object();
                Func<IReadOnlyList<string>, Task> announce = async changed =>
                {
                    await deliveryEnabled.Task.ConfigureAwait(false);
                    foreach (string each in changed)
                    {
                        await SendAsync(
                                server,
                                NotificationMethods.ResourceUpdatedNotification,
                                new JsonObject { ["uri"] = each },
                                subscriptionId,
                                CancellationToken.None,
                                tolerateClosedTransport: true)
                            .ConfigureAwait(false);
                    }
                };

                foreach (string uri in watched)
                {
                    await watcher.SubscribeAsync(
                            uri,
                            subscriberKey,
                            announce,
                            tmux,
                            cancellationToken)
                        .ConfigureAwait(false);
                    subscribed.Add(uri);
                }
            }

            // The watcher is ready before the grant, while its delivery gate
            // keeps the acknowledgement first on the wire.
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
            deliveryEnabled.TrySetResult();

            // The response is the stream. Returning early would close it and
            // take the subscription with it, so this waits out the request.
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client stopped listening, which is how a listen ends.
        }
        finally
        {
            deliveryEnabled.TrySetCanceled(cancellationToken);
            if (watcher is not null && subscriberKey is not null)
            {
                foreach (string uri in subscribed)
                {
                    await watcher.UnsubscribeAsync(uri, subscriberKey).ConfigureAwait(false);
                }
            }
        }

        return new EmptyResult();
    }

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
        RequestId subscriptionId,
        CancellationToken cancellationToken,
        bool tolerateClosedTransport = false)
    {
        parameters["_meta"] = new JsonObject
        {
            [MetaKeys.SubscriptionId] = subscriptionId.Id switch
            {
                string text => JsonValue.Create(text),
                long number => JsonValue.Create(number),
                _ => throw new InvalidOperationException("The subscription id is invalid."),
            },
        };

        try
        {
            await server.SendNotificationAsync(method, parameters, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (tolerateClosedTransport
            && error is (IOException or ObjectDisposedException
                or InvalidOperationException or OperationCanceledException))
        {
            // The client hung up mid-stream. The subscription dies with it.
        }
    }
}
