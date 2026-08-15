namespace LibTmux;

/// <summary>Thrown when an expression cannot be translated to a query.</summary>
/// <remarks>
/// Translation never falls back to client evaluation. Silently evaluating an
/// untranslatable node would make one predicate mean different things
/// depending on where it ran.
/// </remarks>
public sealed class UnsupportedQueryExpressionException : LibTmuxException
{
    /// <summary>Initializes the exception for one untranslatable expression.</summary>
    /// <param name="message">What could not be translated.</param>
    public UnsupportedQueryExpressionException(string message)
        : base(message) => Expression = string.Empty;

    /// <summary>Initializes the exception naming the expression it refused.</summary>
    /// <param name="message">What could not be translated.</param>
    /// <param name="expression">The expression, as the caller wrote it.</param>
    /// <param name="innerException">The failure underneath, when there is one.</param>
    /// <remarks>
    /// A refusal that does not name what it refused leaves the caller to find
    /// it by bisecting the predicate, which is the whole predicate's work.
    /// </remarks>
    public UnsupportedQueryExpressionException(
        string message,
        string expression,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentNullException.ThrowIfNull(expression);
        Expression = expression;
    }

    /// <summary>Gets the expression that could not be translated.</summary>
    /// <remarks>Empty when the refusal named no single expression.</remarks>
    public string Expression { get; }
}
