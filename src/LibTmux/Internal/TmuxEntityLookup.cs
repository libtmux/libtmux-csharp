namespace LibTmux.Internal;

/// <summary>Resolves stable entity identifiers without materializing collections.</summary>
internal sealed class TmuxEntityLookup(
    Func<IReadOnlyList<string>, CancellationToken, Task<TmuxCommandResult>> execute)
{
    private const string GenerationFormat = "#{pid}:#{start_time}";

    internal async Task<(ServerGeneration Generation, SessionId Id)?> FindSessionAsync(
        SessionId id,
        CancellationToken cancellationToken)
    {
        TmuxCommandResult result = await execute(
            ["list-sessions", "-F", $"{GenerationFormat}\t#{{session_id}}"],
            cancellationToken).ConfigureAwait(false);
        EnsureSuccessful(result, "session lookup");
        foreach (string line in result.StandardOutputLines)
        {
            (ServerGeneration Generation, string Text) fields = ParseIdentityRow(line, "session");
            if (!SessionId.TryParse(fields.Text, out SessionId candidate))
            {
                throw new InvalidDataException("tmux reported a malformed session identifier.");
            }

            if (candidate == id)
            {
                return (fields.Generation, candidate);
            }
        }

        return null;
    }

    internal async Task<(ServerGeneration Generation, WindowId Id)?> FindWindowAsync(
        WindowId id,
        CancellationToken cancellationToken)
    {
        TmuxCommandResult result = await execute(
            ["list-windows", "-a", "-F", $"{GenerationFormat}\t#{{window_id}}"],
            cancellationToken).ConfigureAwait(false);
        EnsureSuccessful(result, "window lookup");
        var seen = new HashSet<(ServerGeneration Generation, WindowId Id)>();
        foreach (string line in result.StandardOutputLines)
        {
            (ServerGeneration Generation, string Text) fields = ParseIdentityRow(line, "window");
            if (!WindowId.TryParse(fields.Text, out WindowId candidate))
            {
                throw new InvalidDataException("tmux reported a malformed window identifier.");
            }

            var identity = (fields.Generation, candidate);
            if (seen.Add(identity) && candidate == id)
            {
                return identity;
            }
        }

        return null;
    }

    internal async Task<(ServerGeneration Generation, PaneId Id)?> FindPaneAsync(
        PaneId id,
        CancellationToken cancellationToken)
    {
        TmuxCommandResult result = await execute(
            ["list-panes", "-a", "-F", $"{GenerationFormat}\t#{{pane_id}}"],
            cancellationToken).ConfigureAwait(false);
        EnsureSuccessful(result, "pane lookup");
        var seen = new HashSet<(ServerGeneration Generation, PaneId Id)>();
        foreach (string line in result.StandardOutputLines)
        {
            (ServerGeneration Generation, string Text) fields = ParseIdentityRow(line, "pane");
            if (!PaneId.TryParse(fields.Text, out PaneId candidate))
            {
                throw new InvalidDataException("tmux reported a malformed pane identifier.");
            }

            var identity = (fields.Generation, candidate);
            if (seen.Add(identity) && candidate == id)
            {
                return identity;
            }
        }

        return null;
    }

    private static (ServerGeneration Generation, string Text) ParseIdentityRow(
        string line,
        string kind)
    {
        string[] fields = line.Split('\t');
        if (fields.Length != 2)
        {
            throw new InvalidDataException($"tmux reported a malformed {kind} identity row.");
        }

        return (TmuxConnection.ParseGeneration(fields[0]), fields[1]);
    }

    private static void EnsureSuccessful(TmuxCommandResult result, string operation)
    {
        if (result.ExitCode != 0 || result.StandardErrorLines.Count > 0)
        {
            throw new TmuxCommandException($"{operation} failed.", result);
        }
    }
}
