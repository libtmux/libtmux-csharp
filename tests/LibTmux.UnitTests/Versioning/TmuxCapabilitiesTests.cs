using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using LibTmux.Internal;

namespace LibTmux.UnitTests.Versioning;

public sealed class TmuxCapabilitiesTests
{
    [Fact]
    public void Comparisons_cover_equal_older_and_newer_versions()
    {
        TmuxVersion expected = TmuxVersion.Parse("3.7a");
        TmuxVersion equal = TmuxVersion.Parse("3.7a");
        TmuxVersion older = TmuxVersion.Parse("3.6");
        TmuxVersion newer = TmuxVersion.Parse("3.7b");

        Assert.Equal(0, expected.CompareTo(equal));
        Assert.True(older < expected);
        Assert.True(newer > expected);
    }

    [Theory]
    [InlineData("3.7", 3, 7, null)]
    [InlineData("3.7b", 3, 7, "b")]
    [InlineData("3.0-rc3", 3, 0, "rc3")]
    [InlineData("3.3a-openbsd", 3, 3, "a-openbsd")]
    [InlineData("3.7-openbsd", 3, 7, "openbsd")]
    [InlineData("3.7-dev", 3, 7, "dev")]
    [InlineData("3.7-dev.0", 3, 7, "dev.0")]
    [InlineData("next-3.8", 3, 8, "next")]
    public void Parsing_preserves_every_canonical_projection(
        string raw,
        int major,
        int minor,
        string? suffix)
    {
        TmuxVersion version = TmuxVersion.Parse(raw);

        Assert.True(version.IsValid);
        Assert.Equal(raw, version.Raw);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(suffix, version.Suffix);
        Assert.Equal(raw, version.ToString());
        Assert.Equal(version, new TmuxVersion(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" 3.7")]
    [InlineData("3.7 ")]
    [InlineData("tmux 3.7")]
    [InlineData("master")]
    [InlineData("03.7")]
    [InlineData("3.07")]
    [InlineData("3.7B")]
    [InlineData("3.7.1")]
    [InlineData("3.7-")]
    [InlineData("+3.7")]
    [InlineData("2147483648.7")]
    [InlineData("3.2147483648")]
    [InlineData("3.7-rc0")]
    [InlineData("3.7-rc01")]
    [InlineData("3.7-dev.01")]
    [InlineData("next-3.7a")]
    public void Parsing_rejects_noncanonical_or_overflowing_input(string text)
    {
        Assert.Throws<FormatException>(() => new TmuxVersion(text));
        Assert.Throws<FormatException>(() => TmuxVersion.Parse(text));
        Assert.False(TmuxVersion.TryParse(text, out TmuxVersion result));
        Assert.Equal(default, result);
    }

    [Fact]
    public void Parsing_distinguishes_null_and_normalizes_default()
    {
        TmuxVersion version = default;

        Assert.False(version.IsValid);
        Assert.Equal(string.Empty, version.Raw);
        Assert.Equal(0, version.Major);
        Assert.Equal(0, version.Minor);
        Assert.Null(version.Suffix);
        Assert.Equal(string.Empty, version.ToString());
        Assert.Equal(default, version);
        Assert.Equal(default(TmuxVersion).GetHashCode(), version.GetHashCode());
        Assert.Throws<ArgumentNullException>(() => new TmuxVersion(null!));
        Assert.Throws<ArgumentNullException>(() => TmuxVersion.Parse(null!));
        Assert.False(TmuxVersion.TryParse(null, out TmuxVersion parsed));
        Assert.Equal(default, parsed);
    }

    [Theory]
    [InlineData("next-3.7", "3.7-dev")]
    [InlineData("3.7-dev", "3.7-dev.0")]
    [InlineData("3.7-dev.0", "3.7-rc1")]
    [InlineData("3.7-rc1", "3.7-rc2")]
    [InlineData("3.7-rc2", "3.7")]
    [InlineData("3.7", "3.7-openbsd")]
    [InlineData("3.7-openbsd", "3.7a")]
    [InlineData("3.7a", "3.7a-openbsd")]
    [InlineData("3.7a-openbsd", "3.7b")]
    [InlineData("3.7z", "3.7aa")]
    [InlineData("3.7b", "next-3.8")]
    [InlineData("next-3.8", "3.8")]
    [InlineData("3.9", "4.0")]
    public void Ordering_follows_the_frozen_total_order(string olderRaw, string newerRaw)
    {
        TmuxVersion older = TmuxVersion.Parse(olderRaw);
        TmuxVersion newer = TmuxVersion.Parse(newerRaw);

        Assert.True(older < newer);
        Assert.True(older <= newer);
        Assert.True(newer > older);
        Assert.True(newer >= older);
        Assert.True(older.IsAtLeast(older));
        Assert.False(older.IsAtLeast(newer));
        Assert.NotEqual(0, older.CompareTo(newer));
        Assert.Equal(-older.CompareTo(newer), newer.CompareTo(older));
    }

    [Fact]
    public void Ordered_operations_reject_invalid_operands()
    {
        TmuxVersion valid = TmuxVersion.Parse("3.7");
        TmuxVersion invalid = default;

        Assert.Throws<InvalidOperationException>(() => invalid.CompareTo(valid));
        Assert.Throws<InvalidOperationException>(() => valid.CompareTo(invalid));
        Assert.Throws<InvalidOperationException>(() => invalid < valid);
        Assert.Throws<InvalidOperationException>(() => valid <= invalid);
        Assert.Throws<InvalidOperationException>(() => invalid > valid);
        Assert.Throws<InvalidOperationException>(() => valid >= invalid);
        Assert.Throws<InvalidOperationException>(() => invalid.IsAtLeast(valid));
        Assert.Throws<InvalidOperationException>(() => valid.EnsureAtLeast(invalid));
    }

    [Fact]
    public void Ensure_at_least_retains_required_and_actual_versions()
    {
        TmuxVersion actual = TmuxVersion.Parse("3.2");
        TmuxVersion required = TmuxVersion.Parse("3.2a");

        TmuxVersionTooLowException error = Assert.Throws<TmuxVersionTooLowException>(
            () => actual.EnsureAtLeast(required));

        Assert.Equal(required, error.RequiredVersion);
        Assert.Equal(actual, error.ActualVersion);
        required.EnsureAtLeast(required);
        TmuxVersion.Parse("next-3.8").EnsureAtLeast(required);
    }

    [Fact]
    public void Package_support_metadata_is_inclusive_and_not_a_ceiling()
    {
        Assert.Equal(TmuxVersion.Parse("3.2a"), LibTmuxInfo.MinimumTmuxVersion);
        Assert.Equal(TmuxVersion.Parse("3.7b"), LibTmuxInfo.MaximumTestedTmuxVersion);
        Assert.NotNull(LibTmuxInfo.Version);
        Assert.True(TmuxVersion.Parse("next-3.8").IsAtLeast(LibTmuxInfo.MinimumTmuxVersion));
    }

    [Fact]
    public void Closed_enum_values_and_command_flags_match_tmux()
    {
        Assert.Equal(0, (int)OptionScope.Server);
        Assert.Equal(1, (int)OptionScope.Session);
        Assert.Equal(2, (int)OptionScope.Window);
        Assert.Equal(3, (int)OptionScope.Pane);
        Assert.Equal(0, (int)PaneDirection.Above);
        Assert.Equal(1, (int)PaneDirection.Below);
        Assert.Equal(2, (int)PaneDirection.Left);
        Assert.Equal(3, (int)PaneDirection.Right);
        Assert.Equal(0, (int)ResizeDirection.Up);
        Assert.Equal(1, (int)ResizeDirection.Down);
        Assert.Equal(2, (int)ResizeDirection.Left);
        Assert.Equal(3, (int)ResizeDirection.Right);
        Assert.Equal(0, (int)WindowDirection.Before);
        Assert.Equal(1, (int)WindowDirection.After);

        Assert.Null(CommandFlagCatalog.DefaultOptionScope);
        Assert.Equal("-s", CommandFlagCatalog.GetOptionScopeFlag(OptionScope.Server));
        Assert.Equal(string.Empty, CommandFlagCatalog.GetOptionScopeFlag(OptionScope.Session));
        Assert.Equal("-w", CommandFlagCatalog.GetOptionScopeFlag(OptionScope.Window));
        Assert.Equal("-p", CommandFlagCatalog.GetOptionScopeFlag(OptionScope.Pane));
        Assert.Equal("-g", CommandFlagCatalog.GetHookScopeFlag(OptionScope.Server));
        Assert.Equal(string.Empty, CommandFlagCatalog.GetHookScopeFlag(OptionScope.Session));
        Assert.Equal("-w", CommandFlagCatalog.GetHookScopeFlag(OptionScope.Window));
        Assert.Equal("-p", CommandFlagCatalog.GetHookScopeFlag(OptionScope.Pane));
        Assert.Equal(["-v", "-b"], CommandFlagCatalog.GetPaneDirectionFlags(PaneDirection.Above));
        Assert.Equal(["-v"], CommandFlagCatalog.GetPaneDirectionFlags(PaneDirection.Below));
        Assert.Equal(["-h", "-b"], CommandFlagCatalog.GetPaneDirectionFlags(PaneDirection.Left));
        Assert.Equal(["-h"], CommandFlagCatalog.GetPaneDirectionFlags(PaneDirection.Right));
        Assert.Equal("-U", CommandFlagCatalog.GetResizeDirectionFlag(ResizeDirection.Up));
        Assert.Equal("-D", CommandFlagCatalog.GetResizeDirectionFlag(ResizeDirection.Down));
        Assert.Equal("-L", CommandFlagCatalog.GetResizeDirectionFlag(ResizeDirection.Left));
        Assert.Equal("-R", CommandFlagCatalog.GetResizeDirectionFlag(ResizeDirection.Right));
        Assert.Equal("-b", CommandFlagCatalog.GetWindowDirectionFlag(WindowDirection.Before));
        Assert.Equal("-a", CommandFlagCatalog.GetWindowDirectionFlag(WindowDirection.After));
    }

    [Fact]
    public void Command_flag_catalog_rejects_undefined_values_and_owns_lists()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CommandFlagCatalog.GetOptionScopeFlag((OptionScope)99));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CommandFlagCatalog.GetHookScopeFlag((OptionScope)99));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CommandFlagCatalog.GetPaneDirectionFlags((PaneDirection)99));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CommandFlagCatalog.GetResizeDirectionFlag((ResizeDirection)99));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CommandFlagCatalog.GetWindowDirectionFlag((WindowDirection)99));

        IReadOnlyList<string> flags = CommandFlagCatalog.GetPaneDirectionFlags(
            PaneDirection.Above);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)flags)[0] = "changed");
        Assert.Equal(["-v", "-b"], flags);
    }

    [Fact]
    public void Format_projections_preserve_pinned_python_order()
    {
        string[] clients =
        [
            "client_cwd", "client_height", "client_width", "client_tty",
            "client_termname", "client_created", "client_created_string",
            "client_activity", "client_activity_string", "client_prefix",
            "client_utf8", "client_readonly", "client_session", "client_last_session",
        ];
        string[] panes =
        [
            "history_size", "history_limit", "history_bytes", "pane_index",
            "pane_width", "pane_height", "pane_title", "pane_id", "pane_active",
            "pane_dead", "pane_in_mode", "pane_synchronized", "pane_tty", "pane_pid",
            "pane_start_command", "pane_start_path", "pane_current_path",
            "pane_current_command", "cursor_x", "cursor_y", "scroll_region_upper",
            "scroll_region_lower", "saved_cursor_x", "saved_cursor_y", "alternate_on",
            "alternate_saved_x", "alternate_saved_y", "cursor_flag", "insert_flag",
            "keypad_cursor_flag", "keypad_flag", "wrap_flag", "mouse_standard_flag",
            "mouse_button_flag", "mouse_any_flag", "mouse_utf8_flag", "pane_flags",
            "pane_floating_flag", "pane_x", "pane_y", "pane_z", "pane_zoomed_flag",
            "pane_pb_progress", "pane_pb_state", "pane_pipe_pid", "bracket_paste_flag",
            "synchronized_output_flag",
        ];
        string[] sessions =
        [
            "session_name", "session_windows", "session_width", "session_height",
            "session_id", "session_created", "session_created_string",
            "session_attached", "session_group",
        ];
        string[] windows =
        [
            "window_id", "window_name", "window_width", "window_height",
            "window_layout", "window_panes", "window_index", "window_flags",
            "window_active", "window_bell_flag", "window_activity_flag",
            "window_silence_flag",
        ];

        Assert.Equal(clients, FormatCatalog.ClientProjection.Select(static field => field.WireName));
        Assert.Equal(panes, FormatCatalog.PaneProjection.Select(static field => field.WireName));
        Assert.Equal(sessions, FormatCatalog.SessionProjection.Select(static field => field.WireName));
        Assert.Equal(windows, FormatCatalog.WindowProjection.Select(static field => field.WireName));
        Assert.Equal(82, clients.Concat(panes).Concat(sessions).Concat(windows).Distinct().Count());
    }

    [Fact]
    public void Format_catalog_freezes_versions_scopes_and_clr_names()
    {
        string[] tmux37Fields =
        [
            "pane_flags", "pane_floating_flag", "pane_x", "pane_y", "pane_z",
            "pane_zoomed_flag", "pane_pb_progress", "pane_pb_state", "pane_pipe_pid",
            "bracket_paste_flag", "synchronized_output_flag",
        ];

        foreach (FormatFieldDescriptor field in FormatCatalog.ClientProjection
                     .Concat(FormatCatalog.PaneProjection)
                     .Concat(FormatCatalog.SessionProjection)
                     .Concat(FormatCatalog.WindowProjection))
        {
            TmuxVersion expectedMinimum = tmux37Fields.Contains(field.WireName, StringComparer.Ordinal)
                ? TmuxVersion.Parse("3.7")
                : TmuxVersion.Parse("3.2a");
            Assert.Equal(expectedMinimum, field.MinimumTmuxVersion);
            Assert.Equal(ToPascalCase(field.WireName), field.ClrMemberName);
            Assert.Same(field, FormatCatalog.Resolve(field.WireName));
            Assert.Equal(expectedMinimum, FormatCatalog.GetMinimumTmuxVersion(field.WireName));
            string expectedScope = field.WireName.StartsWith("client_", StringComparison.Ordinal)
                ? "client"
                : field.WireName.StartsWith("session_", StringComparison.Ordinal)
                    ? "session"
                    : field.WireName.StartsWith("window_", StringComparison.Ordinal)
                        ? "window"
                        : "pane";
            Assert.True(field.Scopes.SetEquals([expectedScope]));
        }

        Assert.True(FormatCatalog.GetScopesForListCommand("list-sessions").SetEquals(
            ["universal", "session", "window", "pane"]));
        Assert.True(FormatCatalog.GetScopesForListCommand("list-windows").SetEquals(
            ["universal", "session", "window", "pane"]));
        Assert.True(FormatCatalog.GetScopesForListCommand("list-panes").SetEquals(
            ["universal", "session", "window", "pane"]));
        Assert.True(FormatCatalog.GetScopesForListCommand("list-clients").SetEquals(
            ["universal", "session", "window", "pane", "client"]));
        Assert.Throws<KeyNotFoundException>(() => FormatCatalog.Resolve("not_a_tmux_field"));
        Assert.Throws<KeyNotFoundException>(
            () => FormatCatalog.GetMinimumTmuxVersion("not_a_tmux_field"));
        Assert.Throws<KeyNotFoundException>(
            () => FormatCatalog.GetScopesForListCommand("list-buffers"));
        Assert.Equal("ClientTermname", FormatCatalog.Resolve("client_termname").ClrMemberName);
        Assert.Equal("ClientReadonly", FormatCatalog.Resolve("client_readonly").ClrMemberName);
        Assert.Equal("PanePbState", FormatCatalog.Resolve("pane_pb_state").ClrMemberName);
        Assert.Throws<NotSupportedException>(
            () => ((IList<FormatFieldDescriptor>)FormatCatalog.ClientProjection)[0] =
                FormatCatalog.PaneProjection[0]);
    }

    [Fact]
    public void Format_descriptor_validates_and_copies_ordinal_scopes()
    {
        var source = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pane" };
        var descriptor = new FormatFieldDescriptor(
            "pane_id",
            "PaneId",
            TmuxVersion.Parse("3.2a"),
            source);

        source.Add("window");

        Assert.True(descriptor.Scopes.Contains("pane"));
        Assert.False(descriptor.Scopes.Contains("PANE"));
        Assert.False(descriptor.Scopes.Contains("window"));
        Assert.Throws<ArgumentNullException>(
            () => new FormatFieldDescriptor(null!, "PaneId", TmuxVersion.Parse("3.2a"), source));
        Assert.Throws<ArgumentException>(
            () => new FormatFieldDescriptor(" ", "PaneId", TmuxVersion.Parse("3.2a"), source));
        Assert.Throws<ArgumentNullException>(
            () => new FormatFieldDescriptor("pane_id", null!, TmuxVersion.Parse("3.2a"), source));
        Assert.Throws<ArgumentException>(
            () => new FormatFieldDescriptor("pane_id", " ", TmuxVersion.Parse("3.2a"), source));
        Assert.Throws<ArgumentException>(
            () => new FormatFieldDescriptor("pane_id", "PaneId", default, source));
        Assert.Throws<ArgumentNullException>(
            () => new FormatFieldDescriptor(
                "pane_id",
                "PaneId",
                TmuxVersion.Parse("3.2a"),
                null!));
        Assert.Throws<ArgumentException>(
            () => new FormatFieldDescriptor(
                "pane_id",
                "PaneId",
                TmuxVersion.Parse("3.2a"),
                new HashSet<string>()));
    }

    [Fact]
    public void Capability_profiles_are_exact_and_never_floor_selected()
    {
        string[] approved = ["3.2a", "3.3a", "3.4", "3.5", "3.6", "3.7", "3.7a", "3.7b"];
        foreach (string raw in approved)
        {
            TmuxVersion version = TmuxVersion.Parse(raw);
            Assert.True(TmuxCapabilities.TryGetExact(version, out TmuxCapabilityProfile? profile));
            Assert.NotNull(profile);
            Assert.Equal(version, profile.Version);
            Assert.Same(profile, TmuxCapabilities.GetRequired(version));
        }

        Assert.False(TmuxCapabilities.TryGetExact(TmuxVersion.Parse("3.3"), out _));
        Assert.False(TmuxCapabilities.TryGetExact(TmuxVersion.Parse("next-3.8"), out _));
        Assert.False(TmuxCapabilities.TryGetExact(default, out _));
        Assert.Throws<NotSupportedException>(
            () => TmuxCapabilities.GetRequired(TmuxVersion.Parse("3.3")));
        Assert.Throws<NotSupportedException>(() => TmuxCapabilities.GetRequired(default));
    }

    [Fact]
    public void Capability_profiles_gate_the_exact_37_workaround()
    {
        Assert.Equal(8, TmuxCapabilities.GetRequired(TmuxVersion.Parse("3.2a")).Capabilities.Count);
        Assert.Equal(15, TmuxCapabilities.GetRequired(TmuxVersion.Parse("3.3a")).Capabilities.Count);
        Assert.Equal(23, TmuxCapabilities.GetRequired(TmuxVersion.Parse("3.4")).Capabilities.Count);
        Assert.Equal(24, TmuxCapabilities.GetRequired(TmuxVersion.Parse("3.5")).Capabilities.Count);
        Assert.Equal(29, TmuxCapabilities.GetRequired(TmuxVersion.Parse("3.6")).Capabilities.Count);
        Assert.Equal(39, TmuxCapabilities.GetRequired(TmuxVersion.Parse("3.7")).Capabilities.Count);
        Assert.Equal(38, TmuxCapabilities.GetRequired(TmuxVersion.Parse("3.7a")).Capabilities.Count);
        Assert.Equal(38, TmuxCapabilities.GetRequired(TmuxVersion.Parse("3.7b")).Capabilities.Count);

        // 3.7 gains ten and drops one, so counting alone would read the drop
        // as a smaller gain and never notice it.
        foreach (string carries in new[] { "3.2a", "3.3a", "3.4", "3.5", "3.6" })
        {
            Assert.Contains(
                "choose_tree_sort_time",
                TmuxCapabilities.GetRequired(TmuxVersion.Parse(carries)).Capabilities);
        }

        // The dollar-escape quirk arrives at 3.4 and is gone at 3.5, so it is
        // the one capability that both appears and disappears mid-range.
        Assert.Contains(
            "option_dollar_double_escape",
            TmuxCapabilities.GetRequired(TmuxVersion.Parse("3.4")).Capabilities);
        Assert.DoesNotContain(
            "option_dollar_double_escape",
            TmuxCapabilities.GetRequired(TmuxVersion.Parse("3.3a")).Capabilities);
        Assert.DoesNotContain(
            "option_dollar_double_escape",
            TmuxCapabilities.GetRequired(TmuxVersion.Parse("3.5")).Capabilities);

        // 3.2a advertises the display-message target-client flag in its own
        // usage text and refuses the command carrying it, so the boundary is
        // named rather than counted.
        Assert.DoesNotContain(
            "display_message_client",
            TmuxCapabilities.GetRequired(TmuxVersion.Parse("3.2a")).Capabilities);
        Assert.Contains(
            "display_message_client",
            TmuxCapabilities.GetRequired(TmuxVersion.Parse("3.3a")).Capabilities);

        foreach (string dropped in new[] { "3.7", "3.7a", "3.7b" })
        {
            Assert.DoesNotContain(
                "choose_tree_sort_time",
                TmuxCapabilities.GetRequired(TmuxVersion.Parse(dropped)).Capabilities);
        }

        Assert.True(TmuxCapabilities.GetRequired(
            TmuxVersion.Parse("3.7")).RequiresBreakPane37Workaround);
        Assert.False(TmuxCapabilities.GetRequired(
            TmuxVersion.Parse("3.7a")).RequiresBreakPane37Workaround);
        Assert.False(TmuxCapabilities.GetRequired(
            TmuxVersion.Parse("3.7b")).RequiresBreakPane37Workaround);
    }

    [Fact]
    public void Capability_profiles_copy_and_freeze_their_capability_sets()
    {
        var source = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "feature" };
        var profile = new TmuxCapabilityProfile(TmuxVersion.Parse("3.7"), source);

        source.Add("later");

        Assert.True(profile.Capabilities.Contains("feature"));
        Assert.False(profile.Capabilities.Contains("FEATURE"));
        Assert.False(profile.Capabilities.Contains("later"));
        Assert.Throws<ArgumentException>(() => new TmuxCapabilityProfile(default, source));
        Assert.Throws<ArgumentNullException>(
            () => new TmuxCapabilityProfile(TmuxVersion.Parse("3.7"), null!));
    }

    [Fact]
    public void Server_version_projects_only_materialized_tagged_versions_without_io()
    {
        Server unmaterialized = Server.Open();
        Server tagged = CreateServerWithRawVersion("tmux 3.7b");
        Server malformed = CreateServerWithRawVersion("tmux 3.7 ");
        Server unprefixed = CreateServerWithRawVersion("3.7b");
        Server advisory = CreateServerWithRawVersion("tmux next-3.8");

        Assert.Null(unmaterialized.Version);
        Assert.Equal(TmuxVersion.Parse("3.7b"), tagged.Version);
        Assert.Null(malformed.Version);
        Assert.Null(unprefixed.Version);
        Assert.Null(advisory.Version);
    }

    public static bool IsUnix => !OperatingSystem.IsWindows();

    [Fact(
        Skip = "Requires a Unix process environment.",
        SkipType = typeof(TmuxCapabilitiesTests),
        SkipUnless = nameof(IsUnix))]
    [UnsupportedOSPlatform("windows")]
    public async Task Detection_accepts_one_exact_tmux_line_and_preserves_the_token()
    {
        string executable = CreateExecutable("printf 'tmux 3.7b\\n'");
        try
        {
            Assert.Equal(
                "3.7b",
                await TmuxVersion.DetectStringAsync(
                    executable,
                    TestContext.Current.CancellationToken));
            Assert.Equal(
                TmuxVersion.Parse("3.7b"),
                await TmuxVersion.DetectAsync(
                    executable,
                    TestContext.Current.CancellationToken));
        }
        finally
        {
            DeleteExecutable(executable);
        }
    }

    [Fact(
        Skip = "Requires a Unix process environment.",
        SkipType = typeof(TmuxCapabilitiesTests),
        SkipUnless = nameof(IsUnix))]
    [UnsupportedOSPlatform("windows")]
    public async Task Detection_rejects_malformed_success_output_without_trimming()
    {
        string[] scripts =
        [
            "printf 'tmux 3.7b\\nextra\\n'",
            "printf 'tmux 3.7b\\n\\n'",
            "printf ' tmux 3.7b\\n'",
            "printf 'tmux 3.7b '",
            "printf 'tmux master\\n'",
            "printf '\\377'",
        ];
        foreach (string script in scripts)
        {
            string executable = CreateExecutable(script);
            try
            {
                await Assert.ThrowsAsync<FormatException>(
                    () => TmuxVersion.DetectStringAsync(
                        executable,
                        TestContext.Current.CancellationToken));
            }
            finally
            {
                DeleteExecutable(executable);
            }
        }
    }

    [Theory(
        Skip = "Requires a Unix process environment.",
        SkipType = typeof(TmuxCapabilitiesTests),
        SkipUnless = nameof(IsUnix))]
    [InlineData("printf 'tmux 3.7b\\n'; printf 'warning\\n' >&2", 0)]
    [InlineData("printf 'tmux 3.7b\\n'; exit 7", 7)]
    [UnsupportedOSPlatform("windows")]
    public async Task Detection_maps_command_policy_failures_to_command_exception(
        string script,
        int exitCode)
    {
        string executable = CreateExecutable(script);
        try
        {
            TmuxCommandException error = await Assert.ThrowsAsync<TmuxCommandException>(
                () => TmuxVersion.DetectStringAsync(
                    executable,
                    TestContext.Current.CancellationToken));

            Assert.Equal(exitCode, error.Result.ExitCode);
            Assert.Equal(["-V"], error.Result.Arguments);
        }
        finally
        {
            DeleteExecutable(executable);
        }
    }

    [Fact(
        Skip = "Requires a Unix process environment.",
        SkipType = typeof(TmuxCapabilitiesTests),
        SkipUnless = nameof(IsUnix))]
    [UnsupportedOSPlatform("windows")]
    public async Task Detection_preserves_missing_launch_and_prestart_cancellation_types()
    {
        string missing = Path.Combine(
            Path.GetTempPath(),
            $"libtmux-missing-{Guid.NewGuid():N}");
        TmuxCommandNotFoundException missingError =
            await Assert.ThrowsAsync<TmuxCommandNotFoundException>(
                () => TmuxVersion.DetectAsync(
                    missing,
                    TestContext.Current.CancellationToken));
        Assert.Equal(missing, missingError.TmuxBinaryPath);

        string executable = CreateExecutable("printf 'tmux 3.7b\\n'");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        try
        {
            OperationCanceledException cancellation =
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => TmuxVersion.DetectAsync(executable, canceled.Token));
            Assert.IsNotType<TmuxOperationCanceledException>(cancellation);
            Assert.Equal(canceled.Token, cancellation.CancellationToken);
        }
        finally
        {
            DeleteExecutable(executable);
        }
    }

    private static string ToPascalCase(string wireName) =>
        string.Concat(
            wireName.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(static part => char.ToUpperInvariant(part[0]) + part[1..]));

    private static Server CreateServerWithRawVersion(string rawVersion)
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(Server).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic),
            static candidate =>
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                return parameters.Length == 3
                    && parameters[0].ParameterType == typeof(TmuxConnection);
            });
        var connection = new TmuxConnection(
            new ServerConnectionOptions(),
            static (request, _) => Task.FromResult(
                new TmuxCommandResult(
                    request.LogicalArguments,
                    0,
                    ReadOnlyMemory<byte>.Empty,
                    ReadOnlyMemory<byte>.Empty,
                    [],
                    [])));
        return (Server)constructor.Invoke(
            [connection, new ServerGeneration(1, 1), rawVersion]);
    }

    [UnsupportedOSPlatform("windows")]
    private static string CreateExecutable(string body)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"libtmux-version-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string executable = Path.Combine(directory, "tmux-version");
        File.WriteAllText(executable, $"#!/bin/sh\n{body}\n", new UTF8Encoding(false));
        File.SetUnixFileMode(
            executable,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return executable;
    }

    private static void DeleteExecutable(string executable)
    {
        string? directory = Path.GetDirectoryName(executable);
        if (directory is not null)
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
