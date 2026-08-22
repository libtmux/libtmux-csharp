using LibTmux.Testing;

namespace LibTmux.UnitTests.Testing;

public sealed class TemporaryScopeCleanupTests
{
    [Fact]
    public async Task Disposal_unwinds_child_before_parent()
    {
        List<string> order = [];
        var child = new RecordingDisposable(() => order.Add("child"));
        var parent = new RecordingDisposable(() => order.Add("parent"));

        await TemporaryScopeCleanup.DisposeAsync(child, parent);

        Assert.Equal(["child", "parent"], order);
    }

    [Fact]
    public async Task Parent_cleanup_runs_and_is_attached_when_child_cleanup_fails()
    {
        var childFailure = new IOException("child cleanup failed");
        var parentFailure = new IOException("parent cleanup failed");
        var child = new RecordingDisposable(() => throw childFailure);
        var parent = new RecordingDisposable(() => throw parentFailure);

        IOException thrown = await Assert.ThrowsAsync<IOException>(async () =>
            await TemporaryScopeCleanup.DisposeAsync(child, parent));

        Assert.Same(childFailure, thrown);
        Assert.Contains(parentFailure, thrown.Data.Values.Cast<object>());
        Assert.Equal(1, child.Calls);
        Assert.Equal(1, parent.Calls);
    }

    [Fact]
    public async Task Failed_creation_preserves_primary_and_attaches_cleanup_failure()
    {
        var primary = new InvalidOperationException("creation failed");
        var cleanup = new IOException("server cleanup failed");
        var parent = new RecordingDisposable(() => throw cleanup);

        await TemporaryScopeCleanup.DisposeAfterFailureAsync(parent, primary);

        Assert.Contains(cleanup, primary.Data.Values.Cast<object>());
        Assert.Equal(1, parent.Calls);
    }

    private sealed class RecordingDisposable(Action dispose) : IAsyncDisposable
    {
        internal int Calls { get; private set; }

        public ValueTask DisposeAsync()
        {
            Calls++;
            dispose();
            return ValueTask.CompletedTask;
        }
    }
}
