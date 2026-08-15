using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace LibTmux.Internal;

internal sealed record TmuxCapabilityProfile
{
    internal TmuxCapabilityProfile(
        TmuxVersion version,
        IReadOnlySet<string> capabilities)
    {
        if (!version.IsValid)
        {
            throw new ArgumentException(
                "A capability profile requires a valid tmux version.",
                nameof(version));
        }

        ArgumentNullException.ThrowIfNull(capabilities);
        Version = version;
        Capabilities = capabilities.ToFrozenSet(StringComparer.Ordinal);
    }

    internal TmuxVersion Version { get; }

    internal IReadOnlySet<string> Capabilities { get; }

    internal bool RequiresBreakPane37Workaround =>
        Capabilities.Contains("break_pane_3_7_workaround");
}

internal static class TmuxCapabilities
{
    private static readonly string[] Baseline =
    [
        "attachment_accounting",
        "byte_length_framing",
        "choose_tree_sort_time",
        "control_notifications",
        "format_fields_and_operators",
        "semicolon_grouping",
        "hook_scope_pane_window_set",
        "hook_scope_pane_window_show",
    ];
    private static readonly string[] Added33 =
    [
        "clear_prompt_history_command",
        "command_prompt_background",
        "confirm_before_background",
        "display_message_client",
        "display_popup_3_3_options",
        "server_access_command",
        "show_prompt_history_command",
    ];
    private static readonly string[] Added34 =
    [
        "capture_pane_trim_trailing",
        "option_dollar_double_escape",
        "clear_history_hyperlinks",
        "confirm_before_acceptance",
        "display_menu_styles",
        "display_message_literal",
        "run_shell_working_directory",
        "send_keys_client_keys",
    ];
    private static readonly string[] Added35 =
    [
        "copy_mode_page_down",
        "display_menu_mouse",
    ];
    private static readonly string[] Added36 =
    [
        "capture_pane_mode_screen",
        "command_prompt_literal",
        "display_message_update_pane",
        "display_popup_3_6_key_policy",
        "run_shell_show_stderr",
    ];
    private static readonly string[] Added37 =
    [
        "capture_pane_3_7_metadata",
        "command_prompt_3_7_behavior",
        "kill_session_group",
        "list_keys_format",
        "new_pane_command",
        "paste_buffer_no_vis",
        "refresh_client_clipboard_query",
        "run_shell_arguments",
        "split_window_appearance",
        "split_window_empty",
    ];
    private static readonly FrozenDictionary<TmuxVersion, TmuxCapabilityProfile> Profiles =
        CreateProfiles();

    internal static bool TryGetExact(
        TmuxVersion version,
        [NotNullWhen(true)] out TmuxCapabilityProfile? profile)
    {
        if (!version.IsValid)
        {
            profile = null;
            return false;
        }

        return Profiles.TryGetValue(version, out profile);
    }

    internal static TmuxCapabilityProfile GetRequired(TmuxVersion version) =>
        TryGetExact(version, out TmuxCapabilityProfile? profile)
            ? profile
            : throw new NotSupportedException(
                version.IsValid
                    ? $"tmux {version} has no approved capability profile."
                    : "An invalid tmux version has no approved capability profile.");

    private static FrozenDictionary<TmuxVersion, TmuxCapabilityProfile> CreateProfiles()
    {
        FrozenSet<string> capabilities32 = Freeze(Baseline);
        FrozenSet<string> capabilities33 = Freeze(capabilities32, Added33);
        FrozenSet<string> capabilities34 = Freeze(capabilities33, Added34);
        // tmux 3.4 alone escapes a dollar sign twice when it shows an option
        // back, so the quirk arrives at 3.4 and is gone again at 3.5.
        FrozenSet<string> capabilities35 = Without(
            Freeze(capabilities34, Added35),
            "option_dollar_double_escape");
        FrozenSet<string> capabilities36 = Freeze(capabilities35, Added36);
        // tmux 3.7 dropped the activity-time sort order and rejects it by name.
        FrozenSet<string> capabilities37a = Without(
            Freeze(capabilities36, Added37),
            "choose_tree_sort_time");
        FrozenSet<string> capabilities37 = Freeze(
            capabilities37a,
            ["break_pane_3_7_workaround"]);

        TmuxCapabilityProfile[] profiles =
        [
            Create("3.2a", capabilities32),
            Create("3.3a", capabilities33),
            Create("3.4", capabilities34),
            Create("3.5", capabilities35),
            Create("3.6", capabilities36),
            Create("3.7", capabilities37),
            Create("3.7a", capabilities37a),
            Create("3.7b", capabilities37a),
        ];
        return profiles.ToFrozenDictionary(static profile => profile.Version);
    }

    private static TmuxCapabilityProfile Create(
        string rawVersion,
        IReadOnlySet<string> capabilities) =>
        new(TmuxVersion.Parse(rawVersion), capabilities);

    private static FrozenSet<string> Freeze(
        IEnumerable<string> existing,
        IEnumerable<string>? additions = null) =>
        additions is null
            ? existing.ToFrozenSet(StringComparer.Ordinal)
            : existing.Concat(additions).ToFrozenSet(StringComparer.Ordinal);

    // Capability sets are otherwise additive, because tmux almost only gains
    // flags. A flag it drops still has to be expressible, or the version that
    // dropped it is described as still carrying it.
    private static FrozenSet<string> Without(
        IEnumerable<string> existing,
        params string[] removals) =>
        existing.Except(removals, StringComparer.Ordinal).ToFrozenSet(StringComparer.Ordinal);
}
