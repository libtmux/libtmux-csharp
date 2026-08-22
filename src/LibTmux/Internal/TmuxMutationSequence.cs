namespace LibTmux.Internal;

internal sealed class TmuxMutationSequence
{
    private static readonly object PartialFailureDataKey = new();

    internal const string PartialFailureMessage =
        "An earlier tmux mutation succeeded, but a later step failed. "
        + "tmux state may already have changed; do not retry the whole operation.";

    private readonly string _partialFailureMessage;
    private bool _mutationSucceeded;

    internal TmuxMutationSequence(string? partialFailureMessage = null) =>
        _partialFailureMessage = partialFailureMessage ?? PartialFailureMessage;

    internal async Task MutateAsync(Func<Task> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        try
        {
            await mutation().ConfigureAwait(false);
            _mutationSucceeded = true;
        }
        catch (Exception error)
        {
            ThrowIfPartial(error);
            throw;
        }
    }

    internal async Task<T> MutateAsync<T>(
        Func<Task<T>> mutation,
        Action<T> validate)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(validate);
        bool hadSuccessfulMutation = _mutationSucceeded;
        T value;
        try
        {
            value = await mutation().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            ThrowIfPartial(error);
            throw;
        }

        _mutationSucceeded = true;
        try
        {
            validate(value);
            return value;
        }
        catch (LibTmuxException error) when (IsPartialFailure(error))
        {
            throw;
        }
        catch (LibTmuxException) when (!hadSuccessfulMutation)
        {
            throw;
        }
        catch (Exception error)
        {
            throw PartialFailure(error);
        }
    }

    internal Task<T> MutateAsync<T>(Func<Task<T>> mutation) =>
        MutateAsync(mutation, static _ => { });

    internal async Task<T> ObserveAsync<T>(Func<Task<T>> observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        try
        {
            return await observation().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            ThrowIfPartial(error);
            throw;
        }
    }

    internal async Task ObserveAsync(Func<Task> observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        try
        {
            await observation().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            ThrowIfPartial(error);
            throw;
        }
    }

    internal T Observe<T>(Func<T> observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        try
        {
            return observation();
        }
        catch (Exception error)
        {
            ThrowIfPartial(error);
            throw;
        }
    }

    internal static async Task<T> RunAsync<T>(
        Func<Task> mutation,
        Func<Task<T>> observation)
    {
        var sequence = new TmuxMutationSequence();
        await sequence.MutateAsync(mutation).ConfigureAwait(false);
        return await sequence.ObserveAsync(observation).ConfigureAwait(false);
    }

    private void ThrowIfPartial(Exception error)
    {
        if (!_mutationSucceeded || IsPartialFailure(error))
        {
            return;
        }

        throw PartialFailure(error);
    }

    private LibTmuxException PartialFailure(Exception error)
    {
        var failure = new LibTmuxException(
            _partialFailureMessage,
            TmuxDispatchState.Unknown,
            error);
        failure.Data[PartialFailureDataKey] = true;
        return failure;
    }

    private static bool IsPartialFailure(Exception error) =>
        error is LibTmuxException failure
        && failure.Data[PartialFailureDataKey] is true;
}
