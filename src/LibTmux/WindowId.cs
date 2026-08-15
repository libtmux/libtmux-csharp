using System.Globalization;

namespace LibTmux;

/// <summary>Represents a generation-independent tmux window identifier.</summary>
public readonly record struct WindowId
{
    /// <summary>Initializes a window identifier.</summary>
    public WindowId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the nonnegative numeric value.</summary>
    public int Value { get; }

    /// <summary>Parses a prefixed window identifier.</summary>
    public static WindowId Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out WindowId result)
            ? result
            : throw new FormatException("The value is not a canonical window identifier.");
    }

    /// <summary>Tries to parse a prefixed window identifier.</summary>
    public static bool TryParse(string? text, out WindowId result)
    {
        if (text is not null
            && text.Length > 1
            && text[0] == '@'
            && int.TryParse(text.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int value))
        {
            result = new WindowId(value);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>Returns the canonical prefixed identifier.</summary>
    public override string ToString() => $"@{Value.ToString(CultureInfo.InvariantCulture)}";
}
