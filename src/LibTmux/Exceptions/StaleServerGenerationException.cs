namespace LibTmux;

/// <summary>Reports a stale server generation.</summary>
public sealed class StaleServerGenerationException : InvalidOperationException
{
    /// <summary>Initializes a stale-generation exception.</summary>
    public StaleServerGenerationException(
        string message,
        ServerGeneration expected,
        ServerGeneration actual,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Expected = expected;
        Actual = actual;
    }

    /// <summary>Gets the generation expected by the stale handle.</summary>
    public ServerGeneration Expected { get; }

    /// <summary>Gets the generation currently serving the endpoint.</summary>
    public ServerGeneration Actual { get; }
}
