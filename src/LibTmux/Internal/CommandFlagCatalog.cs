using System.Collections.ObjectModel;

namespace LibTmux.Internal;

internal static class CommandFlagCatalog
{
    private static readonly ReadOnlyCollection<string> PaneAbove =
        Array.AsReadOnly(["-v", "-b"]);
    private static readonly ReadOnlyCollection<string> PaneBelow =
        Array.AsReadOnly(["-v"]);
    private static readonly ReadOnlyCollection<string> PaneLeft =
        Array.AsReadOnly(["-h", "-b"]);
    private static readonly ReadOnlyCollection<string> PaneRight =
        Array.AsReadOnly(["-h"]);

    internal static OptionScope? DefaultOptionScope => null;

    internal static string GetOptionScopeFlag(OptionScope scope) => scope switch
    {
        OptionScope.Server => "-s",
        OptionScope.Session => string.Empty,
        OptionScope.Window => "-w",
        OptionScope.Pane => "-p",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown option scope."),
    };

    internal static string GetHookScopeFlag(OptionScope scope) => scope switch
    {
        OptionScope.Server => "-g",
        OptionScope.Session => string.Empty,
        OptionScope.Window => "-w",
        OptionScope.Pane => "-p",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown hook scope."),
    };

    internal static IReadOnlyList<string> GetPaneDirectionFlags(PaneDirection direction) =>
        direction switch
        {
            PaneDirection.Above => PaneAbove,
            PaneDirection.Below => PaneBelow,
            PaneDirection.Left => PaneLeft,
            PaneDirection.Right => PaneRight,
            _ => throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "Unknown pane direction."),
        };

    internal static string GetResizeDirectionFlag(ResizeDirection direction) => direction switch
    {
        ResizeDirection.Up => "-U",
        ResizeDirection.Down => "-D",
        ResizeDirection.Left => "-L",
        ResizeDirection.Right => "-R",
        _ => throw new ArgumentOutOfRangeException(
            nameof(direction),
            direction,
            "Unknown resize direction."),
    };

    internal static string GetWindowDirectionFlag(WindowDirection direction) => direction switch
    {
        WindowDirection.Before => "-b",
        WindowDirection.After => "-a",
        _ => throw new ArgumentOutOfRangeException(
            nameof(direction),
            direction,
            "Unknown window direction."),
    };
}
