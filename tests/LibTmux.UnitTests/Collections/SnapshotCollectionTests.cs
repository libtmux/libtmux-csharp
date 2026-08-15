namespace LibTmux.UnitTests;

public sealed class SnapshotCollectionTests
{
    private static IReadOnlyList<string> Captured => ["alpha", "beta", "gamma"];

    [Fact]
    public void Enumeration_is_local_and_uses_BCL_cardinality()
    {
        IReadOnlyList<string> snapshot = Captured;
        // A snapshot is an ordinary in-memory sequence, so BCL cardinality and
        // LINQ filtering apply unchanged and never reach tmux.
        Assert.Equal(3, snapshot.Count);
        Assert.Contains(snapshot, static value => value.StartsWith('b'));
        Assert.Equal("alpha", snapshot[0]);
        Assert.Equal("beta", snapshot.Single(static value => value.StartsWith('b')));
        Assert.Null(snapshot.FirstOrDefault(static value => value.StartsWith('z')));
        Assert.Null(snapshot.SingleOrDefault(static value => value.StartsWith('z')));
        Assert.Throws<InvalidOperationException>(
            () => snapshot.Single(static value => value.Length == 5));

        SnapshotLookup<char, string> byInitial =
            snapshot.ToLookupByKey(static value => value[0]);

        Assert.Equal(3, byInitial.Count);
        Assert.Equal("gamma", byInitial['g']);
        Assert.True(byInitial.TryGetValue('a', out string? alpha));
        Assert.Equal("alpha", alpha);
        Assert.False(byInitial.TryGetValue('z', out _));
        Assert.Throws<KeyNotFoundException>(() => byInitial['z']);
    }

    [Fact]
    public void Indexing_rejects_a_key_two_elements_share()
    {
        Assert.Throws<ArgumentException>(
            () => Captured.ToLookupByKey(static value => value.Length));
    }
}
