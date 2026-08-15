using System.Globalization;

namespace LibTmux;

/// <summary>Represents a generation-independent tmux pane identifier.</summary>
public readonly record struct PaneId
{
    /// <summary>Initializes a pane identifier.</summary>
    public PaneId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the nonnegative numeric value.</summary>
    public int Value { get; }

    /// <summary>Parses a prefixed pane identifier.</summary>
    public static PaneId Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out PaneId result)
            ? result
            : throw new FormatException("The value is not a canonical pane identifier.");
    }

    /// <summary>Tries to parse a prefixed pane identifier.</summary>
    public static bool TryParse(string? text, out PaneId result)
    {
        if (text is not null
            && text.Length > 1
            && text[0] == '%'
            && int.TryParse(text.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int value))
        {
            result = new PaneId(value);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>Returns the canonical prefixed identifier.</summary>
    public override string ToString() => $"%{Value.ToString(CultureInfo.InvariantCulture)}";
}
