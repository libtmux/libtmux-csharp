using System.Runtime.Versioning;
using LibTmux.Internal;

namespace LibTmux;

/// <summary>Names whether a pane accepts input.</summary>
public enum PaneInputMode
{
    /// <summary>The pane accepts input.</summary>
    Enable = 0,

    /// <summary>The pane ignores input.</summary>
    Disable = 1,
}

// Moves between a window's panes.
public sealed partial class Window
{
    /// <summary>Selects the pane that was last active.</summary>
    /// <param name="inputMode">Whether to change the pane's input handling instead.</param>
    /// <param name="keepZoom">Whether a zoomed pane stays zoomed.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>The pane that is active afterwards, or null when none is.</returns>
    /// <remarks>
    /// Asking for an input change makes tmux apply that to the last pane and
    /// leave the active pane alone, so the handle that comes back is the pane
    /// that was already active.
    /// </remarks>
    [UnsupportedOSPlatform("windows")]
    public async Task<Pane?> SelectLastPaneAsync(
        PaneInputMode? inputMode = null,
        bool keepZoom = false,
        CancellationToken cancellationToken = default)
    {
        List<string> arguments = ["last-pane", "-t", _id.ToString()];
        if (inputMode is PaneInputMode mode)
        {
            arguments.Add(mode == PaneInputMode.Enable ? "-e" : "-d");
        }

        if (keepZoom)
        {
            arguments.Add("-Z");
        }

        return await TmuxMutationSequence.RunAsync(
                () => RunAsync(arguments, cancellationToken),
                async () =>
                {
                    IReadOnlyList<Pane> panes = await GetPanesAsync(cancellationToken)
                        .ConfigureAwait(false);
                    return panes.FirstOrDefault(pane => pane.Snapshot?["pane_active"] == "1");
                })
            .ConfigureAwait(false);
    }
}
