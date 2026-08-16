namespace LibTmux.Query;

/// <summary>Parses the one legacy lookup spelling this port still carries.</summary>
/// <remarks>
/// <para>
/// Python accepts a whole family of <c>field__operator</c> lookups. Only
/// <c>name__contains</c> survives here, as a migration aid for callers porting
/// keyword-argument filters. New code writes an expression and lets translation
/// build the document.
/// </para>
/// <para>
/// This deliberately diverges from Python: there, an unrecognised
/// operator falls back silently to an exact match. Answering a
/// different question than the one asked is the failure this port
/// refuses.
/// </para>
/// </remarks>
public static class QueryEdgeParser
{
    /// <summary>Parses a <c>name__contains</c> lookup into a query document.</summary>
    /// <param name="target">The tmux object whose name is matched.</param>
    /// <param name="value">The substring the name must contain.</param>
    /// <returns>The equivalent query document.</returns>
    /// <exception cref="UnsupportedQueryExpressionException">
    /// The target has no queryable name field.
    /// </exception>
    public static QueryDocument ParseNameContains(QueryTarget target, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string wireName = target switch
        {
            QueryTarget.Session => "session_name",
            QueryTarget.Window => "window_name",
            QueryTarget.Client => "client_name",
            // tmux gives panes a command and a title, never a name, so there is
            // nothing here for a name lookup to mean.
            _ => throw new UnsupportedQueryExpressionException(
                $"Target '{target}' has no queryable name field."),
        };

        // Python's lookup_contains is ordinal substring containment; a
        // culture-sensitive comparison would change the answer per locale.
        return new QueryDocument(
            QueryDocument.CurrentSchema,
            QueryDocument.CurrentVersion,
            target,
            new StringNode(
                QueryStringOperation.ContainsOrdinal,
                new FieldNode(target, wireName),
                new ConstantNode(new StringConstant(value))));
    }
}
