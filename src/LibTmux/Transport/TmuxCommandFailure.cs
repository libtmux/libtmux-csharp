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

        // Not every tmux failure writes to the error stream. Checking a
        // configuration file reports what is wrong on standard output and then
        // exits non-zero, so a caller reading only the error stream would be
        // told the file was fine.
        string reported = string.Join('\n', result.StandardOutputLines).Trim();
        throw new TmuxCommandException(
            reported.Length == 0
                ? $"{operation} failed with exit code {result.ExitCode}."
                : $"{operation} failed: {reported}",
            result);
    }
}
