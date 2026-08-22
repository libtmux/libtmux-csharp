using System.Text;
using LibTmux.Mcp;

namespace LibTmux.UnitTests;

/// <summary>The rules that decide what a tool may spend, and what it says it spent.</summary>
public sealed class ServerPolicyTests
{
    [Fact]
    public void An_unset_environment_offers_the_middle_tier()
    {
        ServerPolicy policy = ServerPolicy.FromEnvironment(_ => null);

        Assert.Equal(SafetyTier.Mutating, policy.Tier);
        Assert.True(policy.Allows(SafetyTier.ReadOnly));
        Assert.True(policy.Allows(SafetyTier.Mutating));
        Assert.False(policy.Allows(SafetyTier.Destructive));
    }

    [Theory]
    [InlineData("readonly", SafetyTier.ReadOnly)]
    [InlineData("ReadOnly", SafetyTier.ReadOnly)]
    [InlineData("read-only", SafetyTier.ReadOnly)]
    [InlineData("mutating", SafetyTier.Mutating)]
    [InlineData("destructive", SafetyTier.Destructive)]
    public void A_named_tier_is_honoured(string value, SafetyTier expected)
    {
        ServerPolicy policy = ServerPolicy.FromEnvironment(
            name => name == ServerPolicy.SafetyVariable ? value : null);

        Assert.Equal(expected, policy.Tier);
    }

    [Fact]
    public void A_tier_nobody_recognises_falls_to_the_safest_one()
    {
        // Not to the default. A typo must never widen what the server offers.
        ServerPolicy policy = ServerPolicy.FromEnvironment(
            name => name == ServerPolicy.SafetyVariable ? "destrutive" : null);

        Assert.Equal(SafetyTier.ReadOnly, policy.Tier);
    }

    [Theory]
    [InlineData("0.001")]
    [InlineData("99999")]
    [InlineData("not-a-number")]
    [InlineData("NaN")]
    public void An_unusable_ceiling_never_stops_the_server_starting(string value)
    {
        ServerPolicy policy = ServerPolicy.FromEnvironment(
            name => name == ServerPolicy.WaitCeilingVariable ? value : null);

        Assert.InRange(policy.WaitCeiling, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(600));
    }

    [Fact]
    public void An_over_large_request_is_lowered_rather_than_refused()
    {
        ServerPolicy policy = new() { WaitCeiling = TimeSpan.FromSeconds(30) };

        Assert.Equal(TimeSpan.FromSeconds(30), policy.EffectiveTimeout(TimeSpan.FromSeconds(600)));
        Assert.Equal(TimeSpan.FromSeconds(5), policy.EffectiveTimeout(TimeSpan.FromSeconds(5)));
        Assert.Equal(TimeSpan.FromSeconds(30), policy.EffectiveTimeout(null));
        Assert.Equal(TimeSpan.FromSeconds(30), policy.EffectiveTimeout(TimeSpan.Zero));
    }
}

/// <summary>Cutting terminal text to a budget without lying about what was cut.</summary>
public sealed class BoundedTextTests
{
    [Fact]
    public void Text_that_already_fits_is_untouched()
    {
        BoundedText fitted = BoundedText.Fit(["one", "two"], 10, 1000);

        Assert.Equal(["one", "two"], fitted.Lines);
        Assert.False(fitted.Truncated);
        Assert.Equal(0, fitted.DroppedLines);
    }

    [Fact]
    public void The_newest_lines_survive_a_line_budget()
    {
        BoundedText fitted = BoundedText.Fit(["a", "b", "c", "d"], 2, 1000);

        Assert.Equal(["c", "d"], fitted.Lines);
        Assert.True(fitted.Truncated);
        Assert.Equal(2, fitted.DroppedLines);
    }

    [Fact]
    public void A_byte_budget_applies_after_the_line_budget()
    {
        // A joined capture can hold one logical line far wider than the pane,
        // so staying inside a line budget does not imply staying inside a
        // byte budget.
        BoundedText fitted = BoundedText.Fit([new string('x', 100), "short"], 10, 20);

        Assert.Equal([new string('x', 14), "short"], fitted.Lines);
        Assert.True(fitted.Truncated);
        Assert.Equal(0, fitted.DroppedLines);
        Assert.Equal(86, fitted.DroppedBytes);
    }

    [Fact]
    public void An_oversized_multibyte_line_is_clipped_on_a_character_boundary()
    {
        string oversized = string.Concat(Enumerable.Repeat("\U0001f642", 10));

        BoundedText fitted = BoundedText.Fit(["old", oversized], 10, 11);

        Assert.Single(fitted.Lines);
        Assert.Equal("\U0001f642\U0001f642", fitted.Lines[0]);
        Assert.Equal(8, Encoding.UTF8.GetByteCount(string.Join('\n', fitted.Lines)));
        Assert.Equal(1, fitted.DroppedLines);
        Assert.Equal(36, fitted.DroppedBytes);
        Assert.DoesNotContain('\ufffd', fitted.Lines[0]);
    }

    [Fact]
    public void Every_result_obeys_the_utf8_byte_ceiling()
    {
        BoundedText fitted = BoundedText.Fit(["earlier", "\U0001f642abcdef", "new"], 10, 6);

        Assert.Equal(["ef", "new"], fitted.Lines);
        Assert.True(Encoding.UTF8.GetByteCount(string.Join('\n', fitted.Lines)) <= 6);
        Assert.Equal(1, fitted.DroppedLines);
        Assert.Equal(16, fitted.DroppedBytes);
    }

    [Fact]
    public void A_character_that_cannot_fit_is_reported_as_fully_dropped()
    {
        BoundedText fitted = BoundedText.Fit(["\U0001f642"], 10, 1);

        Assert.Empty(fitted.Lines);
        Assert.True(fitted.Truncated);
        Assert.Equal(1, fitted.DroppedLines);
        Assert.Equal(4, fitted.DroppedBytes);
    }

    [Fact]
    public void Dropped_bytes_are_counted_in_utf8()
    {
        BoundedText fitted = BoundedText.Fit(["é", "b"], 1, 1000);

        // Two bytes for the character and one for its line break.
        Assert.Equal(3, fitted.DroppedBytes);
    }

    [Fact]
    public void Nothing_at_all_is_not_a_truncation()
    {
        BoundedText fitted = BoundedText.Fit([], 10, 1000);

        Assert.Empty(fitted.Lines);
        Assert.False(fitted.Truncated);
    }

    [Fact]
    public void A_truncated_result_says_so_before_the_text()
    {
        // A reader who cannot see that lines are missing concludes the pane
        // never printed them.
        string rendered = BoundedText.Fit(["a", "b", "c"], 1, 1000).ToDisplayString();

        Assert.StartsWith("[2 complete earlier lines", rendered, StringComparison.Ordinal);
        Assert.EndsWith("c", rendered, StringComparison.Ordinal);
    }
}

/// <summary>What the client is told before it calls anything.</summary>
public sealed class ServerInstructionsTests
{
    [Fact]
    public void The_guidance_fits_the_budget_it_claims()
    {
        string text = ServerInstructions.Compose(new ServerPolicy(), null);

        Assert.True(
            Encoding.UTF8.GetByteCount(text) <= ServerInstructions.MaxBytes,
            $"instructions are {Encoding.UTF8.GetByteCount(text)} bytes");
    }

    [Fact]
    public void The_active_tier_is_stated_so_a_missing_tool_reads_as_policy()
    {
        string text = ServerInstructions.Compose(
            new ServerPolicy { Tier = SafetyTier.ReadOnly },
            null);

        Assert.Contains("readonly", text, StringComparison.Ordinal);
        Assert.Contains("LIBTMUX_SAFETY", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_callers_own_pane_is_named_when_there_is_one()
    {
        string text = ServerInstructions.Compose(new ServerPolicy(), "%7");

        Assert.Contains("%7", text, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= ServerInstructions.MaxBytes);
    }

    [Fact]
    public void A_hostile_pane_variable_is_dropped_rather_than_refused()
    {
        // The pane id is runtime data nobody here controls. Refusing to start
        // over it would be worse than answering without it.
        string text = ServerInstructions.Compose(new ServerPolicy(), new string('%', 4000));

        Assert.True(Encoding.UTF8.GetByteCount(text) <= ServerInstructions.MaxBytes);
    }

    [Fact]
    public void The_anti_triggers_name_what_must_not_route_here()
    {
        string text = ServerInstructions.Compose(new ServerPolicy(), null);

        Assert.Contains("browser tabs", text, StringComparison.Ordinal);
        Assert.Contains("editor splits", text, StringComparison.Ordinal);
    }
}

/// <summary>Removing this server's bookkeeping without removing anything else.</summary>
public sealed class PaneTextTests
{
    private const string Marker = "@lt_s_0123456789";

    [Fact]
    public void A_bookkeeping_line_is_removed()
    {
        IReadOnlyList<string> kept = PaneText.Scrub(
            ["before", $"; {Marker} \"$__lt\"", "after"],
            paneWidth: 80);

        Assert.Equal(["before", "after"], kept);
    }

    [Fact]
    public void A_marker_split_across_wrapped_rows_is_still_found()
    {
        // tmux stores a wrap as a real line break, so the marker arrives in
        // pieces and matching row by row finds nothing.
        string first = new string('x', 74) + "@lt_s_012";
        string second = "3456789 rest";

        IReadOnlyList<string> kept = PaneText.Scrub(
            [first, second, "after"],
            paneWidth: 83);

        Assert.Equal(["after"], kept);
    }

    [Fact]
    public void Rows_already_joined_do_not_swallow_the_line_beneath_them()
    {
        // A capture already joined with -J must not be re-joined by width:
        // that reads the joined line as still wrapped and swallows the row after it.
        string joined = new string('x', 200) + Marker;

        IReadOnlyList<string> kept = PaneText.Scrub([joined, "mcp-ran"], paneWidth: 80);

        Assert.Equal(["mcp-ran"], kept);
    }

    [Fact]
    public void Ordinary_text_mentioning_the_prefix_survives()
    {
        // The shape is anchored, so prose about the marker is not the marker.
        IReadOnlyList<string> lines = ["lt_r_ is a prefix", "lt_s_nothex99"];

        Assert.Equal(lines, PaneText.Scrub(lines, paneWidth: 80));
    }

    [Fact]
    public void Text_with_nothing_to_remove_is_returned_unchanged()
    {
        IReadOnlyList<string> lines = ["one", "two"];

        Assert.Same(lines, PaneText.Scrub(lines, paneWidth: 80));
    }
}
