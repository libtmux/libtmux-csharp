namespace LibTmux;

/// <summary>A tmux filter expression passed through without translation.</summary>
/// <remarks>
/// tmux evaluates the text itself, so nothing here is validated against the
/// closed field catalog. A malformed or unknown token makes tmux return no
/// rows rather than report an error, which reads as an empty result.
/// </remarks>
/// <param name="Value">The raw tmux <c>-f</c> filter text.</param>
public sealed record UnsafeTmuxFilter(string Value);
