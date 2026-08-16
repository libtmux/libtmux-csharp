using System.ComponentModel;
using ModelContextProtocol.Server;

namespace LibTmux.Mcp;

/// <summary>Workflows that took somebody a while to get right.</summary>
/// <remarks>
/// A prompt is the protocol's way of shipping a procedure rather than a
/// capability. Each of these encodes an ordering that is not obvious from the
/// tool list and that a model reliably gets wrong on its first attempt — which
/// is the whole bar for adding one. The set stays small on purpose.
/// </remarks>
[McpServerPromptType]
public sealed class RecipePrompts
{
    /// <summary>Runs a command and reports what it did.</summary>
    /// <param name="command">The command to run.</param>
    /// <param name="paneId">The pane to run it in.</param>
    /// <returns>The instructions to follow.</returns>
    [McpServerPrompt(Name = "tmux_run_and_report", Title = "Run a command and report the result")]
    [Description("Run a shell command in a tmux pane and report whether it succeeded.")]
    public static string RunAndReport(
        [Description("The shell command to run.")] string command,
        [Description("The pane id to run it in, such as %1. Optional.")] string? paneId = null)
    {
        string target = paneId is null ? string.Empty : $", pane_id=\"{paneId}\"";
        return $"""
            Run this in tmux and report what happened:

                tmux_run(command={command.Replace("\"", "\\\"", StringComparison.Ordinal)}{target})

            Read exit_status, timed_out and output from the result.

            - exit_status is the shell's real status, not a guess from the screen.
              Trust it over anything the output appears to say.
            - If timed_out is true the command is STILL RUNNING in the pane. Do not
              re-run it — that would start a second copy. Either call tmux_run again
              with a longer timeout_seconds, or watch it with tmux_tail_pane.
            - If this may take minutes, stop and use tmux_start_job instead, then
              collect it with tmux_job.

            Do not send keys and then poll tmux_capture_pane. tmux_run already waits.
            """;
    }

    /// <summary>Works out why a pane is stuck or failing.</summary>
    /// <param name="paneId">The pane to look at.</param>
    /// <returns>The instructions to follow.</returns>
    [McpServerPrompt(Name = "tmux_diagnose_pane", Title = "Diagnose a failing pane")]
    [Description("Gather what a tmux pane is doing and propose a cause, without changing anything.")]
    public static string DiagnosePane(
        [Description("The pane id to diagnose, such as %1.")] string paneId) =>
        $"""
        Work out what is wrong in tmux pane {paneId}. Change nothing yet.

        1. tmux_snapshot_pane(pane_id="{paneId}") — content, cursor, size and the
           running command in one call.
        2. Read pane.current_command. A shell means whatever ran has finished; a
           program name means it is still going and may simply be slow.
        3. If pane.dead is true the program exited: the screen shows its last
           output. tmux_respawn_pane restarts it.
        4. If content_truncated is set, call again with a larger max_lines — the
           interesting line may be above what you were shown.
        5. If alternate_screen is true, a full-screen program owns the pane and
           scrollback holds what was there BEFORE it started, not its output.
        6. To watch it change, call tmux_tail_pane and keep the cursor. Do not
           re-capture the whole pane repeatedly.

        Then say what you think is wrong and the single smallest command that
        would confirm it. Do not run that command yet.
        """;

    /// <summary>Builds a working session laid out for a task.</summary>
    /// <param name="sessionName">What to call the session.</param>
    /// <returns>The instructions to follow.</returns>
    [McpServerPrompt(Name = "tmux_build_workspace", Title = "Build a three-pane workspace")]
    [Description("Create a tmux session with an editor, a shell and a log pane.")]
    public static string BuildWorkspace(
        [Description("The name for the new session.")] string sessionName) =>
        $"""
        Build a detached tmux session called "{sessionName}" with three panes:
        editor on top, a shell bottom-left, logs bottom-right.

        1. tmux_create_session(name="{sessionName}", width=200, height=50).
           Give a size: nothing will attach, and a session with no client stays
           at 80x24, which wraps most output. Keep the returned pane_id as A.
        2. tmux_split_pane(pane_id=A, direction="Below") — keep its pane_id as B.
        3. tmux_split_pane(pane_id=B, direction="Right") — keep its pane_id as C.
        4. Label them so a human can tell them apart:
           tmux_set_pane_title on A, B and C.
        5. Start what belongs in each with tmux_send_keys. No wait is needed
           first — tmux buffers keystrokes into the pane whether or not the
           shell has finished drawing.

        Use the pane ids from here on. They survive splits, renames and layout
        changes; window names and indexes do not.
        """;

    /// <summary>Stops what is running in a pane without guessing.</summary>
    /// <param name="paneId">The pane to interrupt.</param>
    /// <returns>The instructions to follow.</returns>
    [McpServerPrompt(Name = "tmux_interrupt_pane", Title = "Interrupt a running command")]
    [Description("Interrupt whatever is running in a tmux pane and confirm it stopped.")]
    public static string InterruptPane(
        [Description("The pane id to interrupt, such as %1.")] string paneId) =>
        $"""
        Stop whatever is running in tmux pane {paneId} and confirm it stopped.

        1. tmux_list_panes and note current_command for {paneId}. That is the
           thing you are trying to change; comparing it before and after is the
           only reliable check.
        2. tmux_send_keys(pane_id="{paneId}", keys="C-c", literal=false) —
           tmux reads C-c as an interrupt only when literal is false.
        3. Read current_command again. Back to a shell means it worked.

        Do not wait on a prompt pattern to decide this: a prompt glyph you did
        not predict reads as failure, and the terminal echoes ^C whenever the
        signal is DELIVERED, whether or not the program died.

        If current_command is unchanged, the program is ignoring the interrupt.
        Stop and ask what to do. Do not escalate to C-\ or kill on your own —
        SIGQUIT can dump core, and killing the pane destroys its scrollback.
        """;
}
