namespace LibTmux;

/// <summary>Indexes captured collections without leaving memory.</summary>
/// <remarks>
/// Ordinary filtering is plain LINQ over the snapshot, so this adds only what
/// LINQ cannot express as cheaply: a keyed index and a duplicate-rejecting
/// key contract.
/// </remarks>
public static class SnapshotCollectionExtensions
{
    /// <summary>Indexes a captured collection by a required key.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The captured element type.</typeparam>
    /// <param name="source">The captured elements.</param>
    /// <param name="keySelector">Reads one element's key.</param>
    /// <returns>The keyed index.</returns>
    /// <exception cref="ArgumentException">Two elements share a key.</exception>
    public static SnapshotLookup<TKey, TValue> ToLookupByKey<TKey, TValue>(
        this IEnumerable<TValue> source,
        Func<TValue, TKey> keySelector)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        var entries = new Dictionary<TKey, TValue>();
        foreach (TValue value in source)
        {
            // A shared key means the selector does not identify an element, so
            // silently keeping one of them would hide the modelling mistake.
            if (!entries.TryAdd(keySelector(value), value))
            {
                throw new ArgumentException(
                    "Two captured elements share one key.",
                    nameof(keySelector));
            }
        }

        return new SnapshotLookup<TKey, TValue>(entries);
    }
}
