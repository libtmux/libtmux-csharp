using System.ComponentModel;
using System.Runtime.Versioning;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <content>Putting keystrokes and text into a pane.</content>
[UnsupportedOSPlatform("windows")]
public sealed partial class WriteTools
{
    /// <summary>Sends keys to a pane.</summary>
    /// <param name="keys">The text or key name to send.</param>
    /// <param name="paneId">The pane, or null for the active one.</param>
    /// <param name="enter">Whether Enter follows.</param>
    /// <param name="literal">Whether the text is sent as typed rather than read as key names.</param>
    /// <param name="suppressHistory">Whether to keep the text out of shell history.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux command.</param>
    /// <returns>What was sent.</returns>
    /// <remarks>
    /// This does not wait and reports nothing about what the keys did, which is
    /// the point: it is for driving a program's interface. A shell command
    /// whose result matters belongs in <c>tmux_run</c>.
    /// </remarks>
    [McpServerTool(Name = "tmux_send_keys", Destructive = false, OpenWorld = true, UseStructuredContent = true)]
    [Description(
        "Send raw keystrokes to a pane and return immediately. Use for driving an "
        + "interactive program — a key in vim, a menu choice, Ctrl-C. Set literal=false "
        + "to send named keys such as C-c, Escape or F5. For running a shell command "
        + "and learning whether it worked, use tmux_run instead; this tool tells you "
        + "nothing about what happened next.")]
    public async Task<ActionResult> SendKeysAsync(
        [Description("The text to type, or a key name such as C-c when literal is false.")]
        string keys,
        [Description("The pane id, such as %1. Omit for the active pane.")]
        string? paneId = null,
        [Description("Press Enter after the keys.")] bool enter = false,
        [Description(
            "Send the text exactly as written. Turn this off to send tmux key names "
            + "such as C-c, Escape, Up or F5.")]
        bool literal = true,
        [Description("Keep the text out of the shell's history. Best-effort.")]
        bool suppressHistory = false,
        [Description("The tmux socket to use. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        Pane pane = await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
            .ConfigureAwait(false);

        await pane.SendKeysAsync(
                new SendKeysRequest(
                    text: keys,
                    enter: enter,
                    literal: literal,
                    suppressHistory: suppressHistory),
                cancellationToken)
            .ConfigureAwait(false);

        return new ActionResult(
            $"Sent {keys.Length} characters to {pane.Id}. "
            + "Read the pane, or use tmux_wait_for_text, to see what they did.",
            PaneId: pane.Id.ToString());
    }

    /// <summary>Sends several keystrokes in order.</summary>
    /// <param name="steps">What to send, in order.</param>
    /// <param name="paneId">The pane, or null for the active one.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>What was sent.</returns>
    [McpServerTool(Name = "tmux_send_keys_batch", Destructive = false, OpenWorld = true, UseStructuredContent = true)]
    [Description(
        "Send several keystrokes to one pane in order, in a single call. Use for a "
        + "short interactive sequence — open a file, move, type, save — instead of "
        + "one call per key.")]
    public async Task<ActionResult> SendKeysBatchAsync(
        [Description("The keystrokes to send, in order.")] IReadOnlyList<KeyStep> steps,
        [Description("The pane id, such as %1. Omit for the active pane.")]
        string? paneId = null,
        [Description("The tmux socket to use. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(steps);
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        Pane pane = await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
            .ConfigureAwait(false);

        foreach (KeyStep step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await pane.SendKeysAsync(
                    new SendKeysRequest(
                        text: step.Keys,
                        enter: step.Enter,
                        literal: step.Literal),
                    cancellationToken)
                .ConfigureAwait(false);

            if (step.DelayMilliseconds is int delay and > 0)
            {
                await Task.Delay(Math.Min(delay, 2000), cancellationToken).ConfigureAwait(false);
            }
        }

        return new ActionResult(
            $"Sent {steps.Count} steps to {pane.Id}.",
            PaneId: pane.Id.ToString());
    }

    /// <summary>Pastes text into a pane without the shell reading it as keys.</summary>
    /// <param name="text">The text to paste.</param>
    /// <param name="paneId">The pane, or null for the active one.</param>
    /// <param name="bracketed">Whether to use bracketed paste.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>What was pasted.</returns>
    /// <remarks>
    /// Bracketed paste tells the program the text was pasted rather than typed,
    /// which is what stops an editor auto-indenting every line of it.
    /// </remarks>
    [McpServerTool(Name = "tmux_paste_text", Destructive = false, OpenWorld = true, UseStructuredContent = true)]
    [Description(
        "Paste a block of text into a pane through a tmux buffer. Use for multi-line "
        + "text, or anything an editor would mangle if typed — bracketed paste stops "
        + "auto-indent. The buffer is deleted afterwards.")]
    public async Task<ActionResult> PasteTextAsync(
        [Description("The text to paste.")] string text,
        [Description("The pane id, such as %1. Omit for the active pane.")]
        string? paneId = null,
        [Description(
            "Tell the program the text was pasted, not typed. Keep this on for "
            + "editors, which otherwise re-indent every line.")]
        bool bracketed = true,
        [Description("The tmux socket to use. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        Pane pane = await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
            .ConfigureAwait(false);

        string buffer = $"libtmux_mcp_{Guid.NewGuid():N}"[..24];
        await server.SetBufferAsync(text, buffer, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await pane.PasteBufferAsync(
                    new PasteBufferRequest(name: buffer, bracketed: bracketed),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            // The buffer is this tool's litter, not the user's clipboard
            // history, so it goes whether the paste worked or not.
            try
            {
                await server.DeleteBufferAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (LibTmuxException)
            {
                // Already gone, or the server went away. Neither is worth
                // replacing the caller's real result with.
            }
        }

        return new ActionResult(
            $"Pasted {text.Length} characters into {pane.Id}.",
            PaneId: pane.Id.ToString());
    }

    /// <summary>Clears a pane's screen, and optionally its scrollback.</summary>
    /// <param name="paneId">The pane, or null for the active one.</param>
    /// <param name="includeHistory">Whether scrollback goes too.</param>
    /// <param name="socketName">The tmux socket, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the tmux commands.</param>
    /// <returns>What was cleared.</returns>
    [McpServerTool(Name = "tmux_clear_pane", Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Clear a pane's visible screen, and optionally its scrollback too. Useful "
        + "before running something whose output you want to read on its own. "
        + "Clearing history cannot be undone and invalidates any tmux_tail_pane "
        + "cursor for that pane.")]
    public async Task<ActionResult> ClearPaneAsync(
        [Description("The pane id, such as %1. Omit for the active pane.")]
        string? paneId = null,
        [Description("Also discard the scrollback. This cannot be undone.")]
        bool includeHistory = false,
        [Description("The tmux socket to use. Omit for the default server.")]
        string? socketName = null,
        CancellationToken cancellationToken = default)
    {
        Server server = await ServerAsync(socketName, cancellationToken).ConfigureAwait(false);
        Pane pane = await TmuxTargets.PaneAsync(server, paneId, cancellationToken)
            .ConfigureAwait(false);

        await pane.ClearAsync(cancellationToken).ConfigureAwait(false);
        if (includeHistory)
        {
            await pane.ClearHistoryAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return new ActionResult(
            includeHistory
                ? $"Cleared {pane.Id} and discarded its scrollback."
                : $"Cleared {pane.Id}.",
            PaneId: pane.Id.ToString());
    }
}

/// <summary>One keystroke in a batch.</summary>
/// <param name="Keys">The text to type, or a key name.</param>
/// <param name="Enter">Whether Enter follows.</param>
/// <param name="Literal">Whether the text is sent as typed rather than read as a key name.</param>
/// <param name="DelayMilliseconds">
/// How long to pause afterwards, for a program that needs a moment to redraw
/// before it will accept the next key. Capped at two seconds.
/// </param>
public sealed record KeyStep(
    string Keys,
    bool Enter = false,
    bool Literal = true,
    int? DelayMilliseconds = null);
