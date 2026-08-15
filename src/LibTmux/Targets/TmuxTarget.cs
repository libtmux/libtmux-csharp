namespace LibTmux.Internal;

internal readonly record struct TmuxTarget(string Value)
{
    internal static TmuxTarget From(SessionId id) => new(id.ToString());

    internal static TmuxTarget From(WindowId id) => new(id.ToString());

    internal static TmuxTarget From(PaneId id) => new(id.ToString());
}
