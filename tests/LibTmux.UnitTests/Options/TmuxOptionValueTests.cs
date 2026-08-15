using LibTmux.Internal;

namespace LibTmux.UnitTests.Options;

public sealed class TmuxOptionValueTests
{
    [Theory]
    [InlineData("on", true)]
    [InlineData("off", false)]
    public void Flag_values_read_as_flags(string raw, bool expected)
    {
        TmuxOptionValue value = OptionParser.ParseValue(raw);

        Assert.Equal(expected ? TmuxOptionState.On : TmuxOptionState.Off, value.State);
        Assert.Equal(expected, value.Boolean);
        Assert.Null(value.Integer);
        Assert.Equal(raw, value.Raw);
    }

    [Fact]
    public void A_missing_value_is_absent_rather_than_empty()
    {
        TmuxOptionValue value = OptionParser.ParseValue(null);

        Assert.Equal(TmuxOptionState.Absent, value.State);
        Assert.Null(value.Raw);
        Assert.Null(value.Boolean);
        Assert.Null(value.Integer);

        // An option set to the empty string is a value tmux holds, not a
        // missing one, so the two cannot collapse together.
        TmuxOptionValue empty = OptionParser.ParseValue(string.Empty);
        Assert.Equal(TmuxOptionState.Value, empty.State);
        Assert.Equal(string.Empty, empty.Raw);
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("50", 50L)]
    [InlineData("9007199254740993", 9007199254740993L)]
    public void Whole_numbers_read_as_numbers(string raw, long expected)
    {
        TmuxOptionValue value = OptionParser.ParseValue(raw);

        Assert.Equal(TmuxOptionState.Value, value.State);
        Assert.Equal(expected, value.Integer);
        Assert.Equal(raw, value.Raw);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("1,2")]
    [InlineData("%50")]
    [InlineData("50s")]
    [InlineData("١٢٣")]
    public void Anything_but_digits_stays_text(string raw)
    {
        TmuxOptionValue value = OptionParser.ParseValue(raw);

        Assert.Equal(TmuxOptionState.Value, value.State);
        Assert.Null(value.Integer);
        Assert.Equal(raw, value.Raw);
    }

    [Fact]
    public void Values_read_in_a_run_keep_their_order()
    {
        IReadOnlyList<TmuxOptionValue> values = OptionParser.ParseValues(["on", null, "7"]);

        Assert.Equal(3, values.Count);
        Assert.True(values[0].Boolean);
        Assert.Equal(TmuxOptionState.Absent, values[1].State);
        Assert.Equal(7L, values[2].Integer);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("''", "")]
    [InlineData("\"a b  c\"", "a b  c")]
    [InlineData("'\"quoted\"'", "\"quoted\"")]
    [InlineData("\"'quoted'\"", "'quoted'")]
    [InlineData("\"a\\\"b\\\\c d\"", "a\"b\\c d")]
    [InlineData("a\\tb", "a\tb")]
    [InlineData("a\\nb", "a\nb")]
    [InlineData("\\101\\102", "AB")]
    [InlineData("\\~home", "~home")]
    public void Escaping_is_undone_the_way_tmux_applied_it(string escaped, string expected) =>
        Assert.Equal(expected, OptionParser.Unescape(escaped));

    [Fact]
    public void A_row_carries_its_name_and_value()
    {
        IReadOnlyList<TmuxOption> options = OptionParser.ParseRows([
            "status-keys vi",
            "message-limit 50",
            "user-keys",
        ]);

        Assert.Equal(3, options.Count);
        Assert.Equal("status-keys", options[0].Name);
        Assert.Equal("vi", options[0].Value.Raw);
        Assert.Null(options[0].Index);
        Assert.Equal(50L, options[1].Value.Integer);

        // tmux prints a name with no value for an option it holds nothing for.
        Assert.Equal("user-keys", options[2].Name);
        Assert.Equal(TmuxOptionState.Absent, options[2].Value.State);
    }

    [Fact]
    public void Array_rows_keep_the_gaps_tmux_reported()
    {
        IReadOnlyList<TmuxOption> options = OptionParser.ParseRows([
            "command-alias[0] split-pane=split-window",
            "command-alias[40] zz=split-window",
        ]);

        Assert.Equal(2, options.Count);
        Assert.All(options, option => Assert.Equal("command-alias", option.Name));
        Assert.Equal(0, options[0].Index);

        // Nothing invents entries 1 through 39 that tmux never reported.
        Assert.Equal(40, options[1].Index);
    }

    [Fact]
    public void An_inherited_marker_is_not_part_of_the_name()
    {
        IReadOnlyList<TmuxOption> options = OptionParser.ParseRows([
            "aggressive-resize* off",
            "terminal-features[1]* screen*:title",
        ]);

        // The name is what a caller would pass back to tmux, and tmux would
        // not take the marker.
        Assert.Equal("aggressive-resize", options[0].Name);
        Assert.False(options[0].Value.Boolean);
        Assert.Equal("terminal-features", options[1].Name);
        Assert.Equal(1, options[1].Index);
        Assert.Equal("screen*:title", options[1].Value.Raw);
    }

    [Fact]
    public void Rows_read_as_arrays_give_a_lone_entry_an_index()
    {
        IReadOnlyList<TmuxOption> sparse = OptionParser.ParseSparse([
            "alert-bell \"display-message 'rang'\"",
            "client-attached[5] refresh-client",
        ]);

        Assert.Equal(0, sparse[0].Index);
        Assert.Equal("display-message 'rang'", sparse[0].Value.Raw);
        Assert.Equal(5, sparse[1].Index);

        // The same rows read plainly leave the lone entry without one.
        Assert.Null(OptionParser.ParseRows(["alert-bell \"display-message 'rang'\""])[0].Index);
    }

    [Fact]
    public void Terminal_features_group_by_terminal()
    {
        IReadOnlyDictionary<string, object?> complex = OptionParser.ParseComplex(
            OptionParser.ParseRows([
                "terminal-features[0] xterm*:clipboard:ccolour:focus",
                "terminal-features[1] screen*:title",
            ]));

        IReadOnlyDictionary<string, IReadOnlyList<string>> features =
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
                complex["terminal-features"]);
        Assert.Equal(["clipboard", "ccolour", "focus"], features["xterm*"]);
        Assert.Equal(["title"], features["screen*"]);
    }

    [Fact]
    public void Terminal_overrides_group_by_terminal_and_capability()
    {
        IReadOnlyDictionary<string, object?> complex = OptionParser.ParseComplex(
            OptionParser.ParseRows([
                "terminal-overrides[0] *256col*:colors=256",
                "terminal-overrides[1] xterm*:XT:Ms=\\\\E]52",
            ]));

        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> overrides =
            Assert.IsAssignableFrom<
                IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
                complex["terminal-overrides"]);
        Assert.Equal(256L, overrides["*256col*"]["colors"]);

        // A capability with no value is present rather than missing, which is
        // what tells it apart from one tmux never mentioned.
        Assert.True(overrides["xterm*"].ContainsKey("XT"));
        Assert.Null(overrides["xterm*"]["XT"]);
        Assert.Equal("\\E]52", overrides["xterm*"]["Ms"]);
    }

    [Fact]
    public void Command_aliases_group_by_alias()
    {
        IReadOnlyDictionary<string, object?> complex = OptionParser.ParseComplex(
            OptionParser.ParseRows([
                "command-alias[0] split-pane=split-window",
                "command-alias[2] \"server-info=show-messages -JT\"",
            ]));

        IReadOnlyDictionary<string, string> aliases =
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(
                complex["command-alias"]);
        Assert.Equal("split-window", aliases["split-pane"]);
        Assert.Equal("show-messages -JT", aliases["server-info"]);
    }

    [Fact]
    public void Ordinary_options_come_back_as_they_went_in()
    {
        IReadOnlyDictionary<string, object?> complex = OptionParser.ParseComplex(
            OptionParser.ParseRows([
                "status-keys vi",
                "user-keys[3] F13",
                "user-keys[9] F14",
            ]));

        // A lone option is its value; an array is the sparse map it really is.
        TmuxOptionValue lone = Assert.IsType<TmuxOptionValue>(complex["status-keys"]);
        Assert.Equal("vi", lone.Raw);

        IReadOnlyDictionary<int, TmuxOptionValue> keys =
            Assert.IsAssignableFrom<IReadOnlyDictionary<int, TmuxOptionValue>>(
                complex["user-keys"]);
        Assert.Equal("F13", keys[3].Raw);
        Assert.Equal("F14", keys[9].Raw);
    }
}
