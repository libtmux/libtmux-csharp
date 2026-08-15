using System.Globalization;

namespace LibTmux;

/// <summary>Represents a generation-independent tmux session identifier.</summary>
public readonly record struct SessionId
{
    /// <summary>Initializes a session identifier.</summary>
    public SessionId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the nonnegative numeric value.</summary>
    public int Value { get; }

    /// <summary>Parses a prefixed session identifier.</summary>
    public static SessionId Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out SessionId result)
            ? result
            : throw new FormatException("The value is not a canonical session identifier.");
    }

    /// <summary>Tries to parse a prefixed session identifier.</summary>
    public static bool TryParse(string? text, out SessionId result)
    {
        if (text is not null
            && text.Length > 1
            && text[0] == '$'
            && int.TryParse(text.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int value))
        {
            result = new SessionId(value);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>Returns the canonical prefixed identifier.</summary>
    public override string ToString() => $"${Value.ToString(CultureInfo.InvariantCulture)}";
}
