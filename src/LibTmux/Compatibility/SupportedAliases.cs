using System.Collections.ObjectModel;

namespace LibTmux.Internal;

/// <summary>What the Python names became here.</summary>
/// <remarks>
/// Python libtmux kept older spellings alongside newer ones and warned when the
/// older was used. Nothing carries both spellings here, so a reader coming from
/// Python needs to be told which name replaced theirs rather than to find the
/// old one missing. The mapping is data so a test can hold it to the promise
/// that every name it lists has a replacement that exists.
/// </remarks>
internal static class SupportedAliases
{
    private static readonly ReadOnlyDictionary<string, string> Replacements =
        new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // The window option aliases were the deprecated spelling of the
            // ordinary option commands with the window flag.
            ["libtmux.window:Window.set_window_option"] =
                "M:LibTmux.TmuxOptions.SetAsync(SetOptionRequest,CancellationToken)",
            ["libtmux.window:Window.show_window_option"] =
                "M:LibTmux.TmuxOptions.GetAsync(GetOptionRequest,CancellationToken)",
            ["libtmux.window:Window.show_window_options"] =
                "M:LibTmux.TmuxOptions.GetAllAsync(GetOptionsRequest?,CancellationToken)",

            // Python raised one exception type per tmux wording. Which wording
            // tmux picks depends on its version rather than on the caller, so
            // the three are one failure carrying what tmux said.
            ["libtmux.exc:OptionError"] = "T:LibTmux.TmuxOptionException",
            ["libtmux.exc:UnknownOption"] = "T:LibTmux.TmuxOptionException",
            ["libtmux.exc:InvalidOption"] = "T:LibTmux.TmuxOptionException",
            ["libtmux.exc:AmbiguousOption"] = "T:LibTmux.TmuxOptionException",

            ["libtmux.exc:LibTmuxException"] = "T:LibTmux.LibTmuxException",
            ["libtmux.exc:TmuxCommandNotFound"] = "T:LibTmux.TmuxCommandNotFoundException",
            ["libtmux.exc:TmuxSessionExists"] = "T:LibTmux.TmuxSessionExistsException",
            ["libtmux.exc:VersionTooLow"] = "T:LibTmux.TmuxVersionTooLowException",
            ["libtmux.exc:WaitTimeout"] = "T:LibTmux.TmuxWaitTimeoutException",
            ["libtmux.exc:WindowError"] = "T:LibTmux.TmuxWindowException",
            ["libtmux.exc:PaneError"] = "T:LibTmux.TmuxPaneException",

            // Not finding a thing is one failure whatever kind of thing it was.
            ["libtmux.exc:ObjectDoesNotExist"] = "T:LibTmux.TmuxObjectNotFoundException",
            ["libtmux.exc:TmuxObjectDoesNotExist"] = "T:LibTmux.TmuxObjectNotFoundException",
            ["libtmux.exc:PaneNotFound"] = "T:LibTmux.TmuxObjectNotFoundException",
            ["libtmux.exc:NoActiveWindow"] = "T:LibTmux.TmuxObjectNotFoundException",
            ["libtmux.exc:NoWindowsExist"] = "T:LibTmux.TmuxObjectNotFoundException",

            // A request that cannot mean anything is refused before it is sent,
            // which is what the framework already has argument exceptions for.
            ["libtmux.exc:BadSessionName"] = "T:System.ArgumentException",
            ["libtmux.exc:RequiresDigitOrPercentage"] = "T:System.ArgumentException",
            ["libtmux.exc:UnknownColorOption"] = "T:System.ArgumentException",
            ["libtmux.exc:AdjustmentDirectionRequiresAdjustment"] = "T:System.ArgumentException",
            ["libtmux.exc:PaneAdjustmentDirectionRequiresAdjustment"] = "T:System.ArgumentException",
            ["libtmux.exc:WindowAdjustmentDirectionRequiresAdjustment"] = "T:System.ArgumentException",

            // Asking one thing and getting several back cannot happen where the
            // answer is a list and the caller chooses how many to take.
            ["libtmux.exc:MultipleObjectsReturned"] = "T:System.InvalidOperationException",
            ["libtmux.exc:MultipleActiveWindows"] = "T:System.InvalidOperationException",

            ["libtmux.exc:NotInsideTmux"] = "T:LibTmux.TmuxObjectNotFoundException",
            ["libtmux.exc:VariableUnpackingError"] = "T:LibTmux.IncompleteSnapshotException",

            // Nothing here was ever released under an older name, so nothing
            // can be deprecated yet.
            ["libtmux.exc:DeprecatedError"] = "T:System.NotSupportedException",
        });

    /// <summary>Every Python name that has a replacement here.</summary>
    internal static IReadOnlyCollection<string> PythonSymbolIds => Replacements.Keys;

    /// <summary>Names what replaced one Python symbol.</summary>
    /// <param name="pythonSymbolId">The Python symbol identifier.</param>
    /// <returns>The replacement's identifier, or null when there is none.</returns>
    internal static string? Replacement(string pythonSymbolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pythonSymbolId);
        return Replacements.TryGetValue(pythonSymbolId, out string? replacement)
            ? replacement
            : null;
    }
}
