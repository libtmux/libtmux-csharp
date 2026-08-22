using System.Runtime.Versioning;
using LibTmux.Mcp;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace LibTmux.UnitTests.Mcp;

[UnsupportedOSPlatform("windows")]
public sealed class SubscriptionStreamTests
{
    [Fact]
    public void Requested_resources_are_distinct_supported_and_canonically_ordered()
    {
        IReadOnlyList<string> watched = SubscriptionStream.Canonicalize(
        [
            "tmux://servers",
            "tmux://hierarchy",
            "tmux://servers",
            "tmux://unsupported",
            "tmux://sessions",
            "tmux://hierarchy",
        ]);

        Assert.Equal(HierarchyWatcher.Watchable, watched);
        Assert.Empty(SubscriptionStream.Canonicalize(null));
    }

    [Fact]
    public void Canonicalization_stops_after_the_fixed_watchable_set_is_found()
    {
        int enumerated = 0;

        IEnumerable<string> Requested()
        {
            foreach (string uri in HierarchyWatcher.Watchable.Reverse())
            {
                enumerated++;
                yield return uri;
            }

            throw new InvalidOperationException("The bounded scan read beyond the full set.");
        }

        Assert.Equal(HierarchyWatcher.Watchable, SubscriptionStream.Canonicalize(Requested()));
        Assert.Equal(HierarchyWatcher.Watchable.Count, enumerated);
    }

    [Fact]
    public void Subscription_ids_are_bounded_after_json_encoding()
    {
        Assert.Equal(256, SubscriptionStream.SubscriptionIdMaxEncodedBytes);
        string boundary = new('a', SubscriptionStream.SubscriptionIdMaxEncodedBytes);

        Assert.Equal(
            new RequestId(boundary),
            SubscriptionStream.ValidateSubscriptionId(new RequestId(boundary)));
        McpException large = Assert.Throws<McpException>(() =>
            SubscriptionStream.ValidateSubscriptionId(new RequestId(boundary + "b")));
        _ = Assert.Throws<McpException>(() =>
            SubscriptionStream.ValidateSubscriptionId(new RequestId(new string('\n', 129))));
        _ = Assert.Throws<McpException>(() =>
            SubscriptionStream.ValidateSubscriptionId(new RequestId(new string('x', 1_000_000))));

        Assert.Contains("JSON-encoded bytes", large.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Numeric_subscription_ids_keep_their_wire_type()
    {
        RequestId numeric = new(long.MaxValue);

        RequestId validated = SubscriptionStream.ValidateSubscriptionId(numeric);

        Assert.Equal(numeric, validated);
        Assert.IsType<long>(validated.Id);
    }

    [Fact]
    public void Full_admission_rejects_before_subscriber_allocation()
    {
        Assert.Equal(8, SubscriptionAdmission.ConcurrentListenLimit);
        SubscriptionAdmission admission = new(2);
        using SubscriptionAdmission.Lease first = admission.Acquire(CancellationToken.None);
        using SubscriptionAdmission.Lease second = admission.Acquire(CancellationToken.None);
        int subscriberAllocations = 0;

        void AllocateSubscriber()
        {
            using SubscriptionAdmission.Lease lease = admission.Acquire(CancellationToken.None);
            subscriberAllocations++;
        }

        McpException full = Assert.Throws<McpException>(AllocateSubscriber);

        Assert.Contains("At most 2", full.Message, StringComparison.Ordinal);
        Assert.Contains("Cancel", full.Message, StringComparison.Ordinal);
        Assert.Equal(0, subscriberAllocations);
        Assert.Equal(2, admission.ActiveCount);
    }

    [Fact]
    public void Releasing_or_disposing_a_lease_allows_reacquisition()
    {
        SubscriptionAdmission admission = new(1);
        SubscriptionAdmission.Lease first = admission.Acquire(CancellationToken.None);

        Assert.Equal(1, admission.ActiveCount);
        first.Dispose();
        first.Dispose();
        Assert.Equal(0, admission.ActiveCount);

        using SubscriptionAdmission.Lease second = admission.Acquire(CancellationToken.None);
        Assert.Equal(1, admission.ActiveCount);
    }

    [Fact]
    public void Precancelled_admission_does_not_consume_capacity()
    {
        SubscriptionAdmission admission = new(1);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() =>
            admission.Acquire(cancellation.Token));

        Assert.Equal(0, admission.ActiveCount);
        using SubscriptionAdmission.Lease available = admission.Acquire(CancellationToken.None);
        Assert.Equal(1, admission.ActiveCount);
    }
}
