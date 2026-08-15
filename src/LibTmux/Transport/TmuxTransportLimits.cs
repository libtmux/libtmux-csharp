namespace LibTmux.Internal;

internal sealed class TmuxTransportLimits
{
    private const int DefaultMaxFramedFieldBytes = 64 * 1024 * 1024;

    internal TmuxTransportLimits(
        int MaxArguments = 4096,
        int MaxCapturedBytesPerStream = 64 * 1024 * 1024,
        TimeSpan? CleanupTimeoutValue = null,
        int? MaxFramedFieldBytesValue = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxArguments);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxCapturedBytesPerStream);

        // A scalar can never outgrow the stream that carries it, so the
        // default follows the capture ceiling instead of fighting it.
        int framedFieldBytes = MaxFramedFieldBytesValue
            ?? Math.Min(DefaultMaxFramedFieldBytes, MaxCapturedBytesPerStream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framedFieldBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            framedFieldBytes,
            MaxCapturedBytesPerStream);

        TimeSpan cleanupTimeout = CleanupTimeoutValue ?? TimeSpan.FromSeconds(5);
        if (cleanupTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(CleanupTimeoutValue));
        }

        this.MaxArguments = MaxArguments;
        this.MaxCapturedBytesPerStream = MaxCapturedBytesPerStream;
        MaxFramedFieldBytes = framedFieldBytes;
        CleanupTimeout = cleanupTimeout;
    }

    internal int MaxArguments { get; }

    internal int MaxCapturedBytesPerStream { get; }

    /// <summary>Gets the largest single length-prefixed scalar accepted.</summary>
    internal int MaxFramedFieldBytes { get; }

    internal TimeSpan CleanupTimeout { get; }
}
