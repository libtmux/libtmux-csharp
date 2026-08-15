using System.Runtime.Versioning;

namespace LibTmux.Internal;

/// <summary>
/// Runs one tmux list command and materializes its framed rows.
/// </summary>
/// <remarks>
/// A single-target lookup asks tmux for the whole listing and selects the row
/// itself. tmux resolves an ambiguous <c>-t</c> against the caller's current
/// session, which a library has no business inheriting, so selection happens
/// here against explicit identifiers instead.
/// </remarks>
internal sealed class MaterializationQuery
{
    private readonly MaterializationContext _context;

    internal MaterializationQuery(MaterializationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>Fetches every row one tmux list command reports.</summary>
    /// <param name="listCommand">A tmux <c>list-*</c> subcommand.</param>
    /// <param name="extraArguments">Arguments appended before <c>-F</c>.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>One decoded field dictionary per row.</returns>
    [UnsupportedOSPlatform("windows")]
    internal async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> FetchAsync(
        string listCommand,
        IEnumerable<string>? extraArguments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listCommand);
        // Reading Generation first rejects an unmaterialized server before a
        // command is ever dispatched.
        ServerGeneration generation = _context.Generation;
        FormatProjection projection = FormatProjection.Create(
            listCommand,
            _context.TmuxVersion);
        string[] arguments =
        [
            listCommand,
            .. extraArguments ?? [],
            "-F",
            projection.Template,
        ];

        TmuxConnection connection = _context.Server.Connection
            ?? throw new InvalidOperationException(
                "The server has no connection; connect before querying.");
        TmuxCommandResult result = await connection
            .CreateEntityDispatcher(generation)
            .ExecuteAsync(arguments, cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new TmuxCommandException($"{listCommand} failed.", result);
        }

        try
        {
            return Materializer.MaterializeFormatFields(
                _context,
                result.StandardOutput.Span,
                listCommand);
        }
        catch (InvalidDataException error)
        {
            throw new TmuxTransportException(
                $"tmux returned an undecodable {listCommand} listing.",
                arguments,
                error);
        }
    }

    /// <summary>Fetches exactly the row whose identifier matches.</summary>
    /// <param name="listCommand">A tmux <c>list-*</c> subcommand.</param>
    /// <param name="idWireName">The identifying format token.</param>
    /// <param name="id">The identifier the row must carry.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The matching row, or null when tmux has no such target.</returns>
    [UnsupportedOSPlatform("windows")]
    internal async Task<IReadOnlyDictionary<string, string?>?> FetchOneAsync(
        string listCommand,
        string idWireName,
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idWireName);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        // "-a" makes window and pane listings span every session, so lookup
        // never depends on which session tmux considers current.
        string[] extra = listCommand is "list-windows" or "list-panes" ? ["-a"] : [];
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows =
            await FetchAsync(listCommand, extra, cancellationToken).ConfigureAwait(false);
        foreach (IReadOnlyDictionary<string, string?> row in rows)
        {
            if (row.TryGetValue(idWireName, out string? candidate)
                && string.Equals(candidate, id, StringComparison.Ordinal))
            {
                return row;
            }
        }

        // A reachable server that lists no matching row is a missing target,
        // which the caller must distinguish from an unreachable server; an
        // unreachable server has already thrown from FetchAsync.
        return null;
    }
}
