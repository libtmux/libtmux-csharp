using ModelContextProtocol;

namespace LibTmux.Mcp;

/// <summary>Bounds the long-lived subscription streams owned by one server.</summary>
internal sealed class SubscriptionAdmission
{
    internal const int ConcurrentListenLimit = 8;

    private readonly object _gate = new();
    private readonly int _capacity;
    private int _active;

    internal SubscriptionAdmission()
        : this(ConcurrentListenLimit)
    {
    }

    internal SubscriptionAdmission(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    internal int ActiveCount
    {
        get
        {
            lock (_gate)
            {
                return _active;
            }
        }
    }

    internal Lease Acquire(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_active >= _capacity)
            {
                throw new McpException(
                    $"At most {_capacity} subscription listeners may be active at once. "
                    + "Cancel an existing subscriptions/listen request before opening another.");
            }

            _active++;
        }

        return new Lease(this);
    }

    private void Release()
    {
        lock (_gate)
        {
            if (_active <= 0)
            {
                throw new InvalidOperationException("No subscription admission is active.");
            }

            _active--;
        }
    }

    internal sealed class Lease : IDisposable
    {
        private SubscriptionAdmission? _owner;

        internal Lease(SubscriptionAdmission owner) => _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();
    }
}
