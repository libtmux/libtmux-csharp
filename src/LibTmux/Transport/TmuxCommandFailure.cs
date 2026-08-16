namespace LibTmux.Internal;

internal static class TmuxCommandFailure
{
    internal static void ThrowIfFailed(TmuxCommandResult result, string operation)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        if (result.StandardErrorLines.Count > 0)
        {
            throw new TmuxCommandException(
                $"{operation} failed: {string.Join('\n', result.StandardErrorLines)}",
                result);
        }

        if (result.ExitCode == 0)
        {
            return;
        }

        // Not every failure writes to standard error: checking a config file
        // reports the problem on standard output but still exits non-zero.
        string reported = string.Join('\n', result.StandardOutputLines).Trim();
        throw new TmuxCommandException(
            reported.Length == 0
                ? $"{operation} failed with exit code {result.ExitCode}."
                : $"{operation} failed: {reported}",
            result);
    }
}
