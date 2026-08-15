using System.Collections.ObjectModel;

namespace LibTmux.Internal;

internal static class TmuxFormats
{
    internal static ReadOnlyCollection<string> Client { get; } = Array.AsReadOnly(
        new[]
        {
            "client_cwd",
            "client_height",
            "client_width",
            "client_tty",
            "client_termname",
            "client_created",
            "client_created_string",
            "client_activity",
            "client_activity_string",
            "client_prefix",
            "client_utf8",
            "client_readonly",
            "client_session",
            "client_last_session",
        });

    internal static ReadOnlyCollection<string> Pane { get; } = Array.AsReadOnly(
        new[]
        {
            "history_size",
            "history_limit",
            "history_bytes",
            "pane_index",
            "pane_width",
            "pane_height",
            "pane_title",
            "pane_id",
            "pane_active",
            "pane_dead",
            "pane_in_mode",
            "pane_synchronized",
            "pane_tty",
            "pane_pid",
            "pane_start_command",
            "pane_start_path",
            "pane_current_path",
            "pane_current_command",
            "cursor_x",
            "cursor_y",
            "scroll_region_upper",
            "scroll_region_lower",
            "saved_cursor_x",
            "saved_cursor_y",
            "alternate_on",
            "alternate_saved_x",
            "alternate_saved_y",
            "cursor_flag",
            "insert_flag",
            "keypad_cursor_flag",
            "keypad_flag",
            "wrap_flag",
            "mouse_standard_flag",
            "mouse_button_flag",
            "mouse_any_flag",
            "mouse_utf8_flag",
            "pane_flags",
            "pane_floating_flag",
            "pane_x",
            "pane_y",
            "pane_z",
            "pane_zoomed_flag",
            "pane_pb_progress",
            "pane_pb_state",
            "pane_pipe_pid",
            "bracket_paste_flag",
            "synchronized_output_flag",
        });

    internal static ReadOnlyCollection<string> Session { get; } = Array.AsReadOnly(
        new[]
        {
            "session_name",
            "session_windows",
            "session_width",
            "session_height",
            "session_id",
            "session_created",
            "session_created_string",
            "session_attached",
            "session_group",
        });

    internal static ReadOnlyCollection<string> Window { get; } = Array.AsReadOnly(
        new[]
        {
            "window_id",
            "window_name",
            "window_width",
            "window_height",
            "window_layout",
            "window_panes",
            "window_index",
            "window_flags",
            "window_active",
            "window_bell_flag",
            "window_activity_flag",
            "window_silence_flag",
        });
}
