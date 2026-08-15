using System.Collections.ObjectModel;

namespace LibTmux;

/// <summary>Contains the inspectable result of one raw tmux command.</summary>
public sealed record TmuxCommandResult
{
    private readonly ReadOnlyCollection<string> _arguments;
    private readonly byte[] _standardOutput;
    private readonly byte[] _standardError;
    private readonly ReadOnlyCollection<string> _standardOutputLines;
    private readonly ReadOnlyCollection<string> _standardErrorLines;

    /// <summary>Initializes a command result.</summary>
    public TmuxCommandResult(
        IReadOnlyList<string> arguments,
        int exitCode,
        ReadOnlyMemory<byte> standardOutput,
        ReadOnlyMemory<byte> standardError,
        IReadOnlyList<string> standardOutputLines,
        IReadOnlyList<string> standardErrorLines)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(standardOutputLines);
        ArgumentNullException.ThrowIfNull(standardErrorLines);

        _arguments = Array.AsReadOnly(arguments.ToArray());
        _standardOutput = standardOutput.ToArray();
        _standardError = standardError.ToArray();
        _standardOutputLines = Array.AsReadOnly(standardOutputLines.ToArray());
        _standardErrorLines = Array.AsReadOnly(standardErrorLines.ToArray());
        ExitCode = exitCode;
    }

    /// <summary>Gets the logical tmux arguments.</summary>
    public IReadOnlyList<string> Arguments => _arguments;

    /// <summary>Gets the client exit code.</summary>
    public int ExitCode { get; }

    /// <summary>Gets the exact standard-output bytes.</summary>
    public ReadOnlyMemory<byte> StandardOutput => _standardOutput.ToArray();

    /// <summary>Gets the exact standard-error bytes.</summary>
    public ReadOnlyMemory<byte> StandardError => _standardError.ToArray();

    /// <summary>Gets the projected standard-output lines.</summary>
    public IReadOnlyList<string> StandardOutputLines => _standardOutputLines;

    /// <summary>Gets the projected standard-error lines.</summary>
    public IReadOnlyList<string> StandardErrorLines => _standardErrorLines;

    /// <inheritdoc />
    public bool Equals(TmuxCommandResult? other) =>
        other is not null
        && ExitCode == other.ExitCode
        && _arguments.SequenceEqual(other._arguments, StringComparer.Ordinal)
        && _standardOutput.AsSpan().SequenceEqual(other._standardOutput)
        && _standardError.AsSpan().SequenceEqual(other._standardError)
        && _standardOutputLines.SequenceEqual(other._standardOutputLines, StringComparer.Ordinal)
        && _standardErrorLines.SequenceEqual(other._standardErrorLines, StringComparer.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ExitCode);
        AddSequence(ref hash, _arguments);
        AddSequence(ref hash, _standardOutput);
        AddSequence(ref hash, _standardError);
        AddSequence(ref hash, _standardOutputLines);
        AddSequence(ref hash, _standardErrorLines);
        return hash.ToHashCode();
    }

    private static void AddSequence<T>(ref HashCode hash, IEnumerable<T> values)
    {
        foreach (T value in values)
        {
            hash.Add(value);
        }
    }
}
