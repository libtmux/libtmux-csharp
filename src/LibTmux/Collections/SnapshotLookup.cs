using System.Diagnostics.CodeAnalysis;

namespace LibTmux;

/// <summary>Indexes a captured collection by a stable key.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The captured element type.</typeparam>
/// <remarks>
/// Building the index is explicit because a snapshot is already in memory:
/// a caller who looks up one element once should pay a scan, not an index.
/// </remarks>
public sealed class SnapshotLookup<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _entries;

    internal SnapshotLookup(Dictionary<TKey, TValue> entries) => _entries = entries;

    /// <summary>Gets the number of indexed elements.</summary>
    public int Count => _entries.Count;

    /// <summary>Gets the element with the given key.</summary>
    /// <param name="key">The key to find.</param>
    /// <returns>The matching element.</returns>
    /// <exception cref="KeyNotFoundException">No element carries the key.</exception>
    public TValue this[TKey key] => _entries[key];

    /// <summary>Tries to get the element with the given key.</summary>
    /// <param name="key">The key to find.</param>
    /// <param name="value">The matching element, when present.</param>
    /// <returns>True when an element carries the key.</returns>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) =>
        _entries.TryGetValue(key, out value);
}
