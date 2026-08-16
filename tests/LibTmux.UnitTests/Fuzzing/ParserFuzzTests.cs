using System.Diagnostics;
using System.Text;
using LibTmux.Internal;

namespace LibTmux.UnitTests.Fuzzing;

/// <summary>Feeds the parsers input tmux would never send, and requires them to cope.</summary>
/// <remarks>
/// Every one of these parsers reads bytes produced by another program. The
/// suite proves what they do with the output a supported tmux actually
/// produces; this asks what they do with everything else — a truncated line, a
/// separator inside a value, an escape at the end of a string, a version from a
/// tmux that does not exist yet.
///
/// The contract is deliberately weak, because a parser is allowed to reject
/// nonsense. What it is not allowed to do is fail in a way the caller cannot
/// catch: an index out of range, a null dereference, an unbounded loop. So each
/// case asserts the call returned or threw something the library documents,
/// within a time bound.
///
/// This is randomized robustness testing over a seeded corpus, not
/// coverage-guided fuzzing. The corpus carries the shapes that have broken
/// parsers before, and the generator explores around them with a fixed seed so
/// a failure is reproducible from the test name alone.
/// </remarks>
public sealed class ParserFuzzTests
{
    /// <summary>How long any single parse may take before it counts as hung.</summary>
    /// <remarks>
    /// Generous on purpose: this is looking for an unbounded loop, not for slow
    /// code, and a shared CI runner is not a quiet machine.
    /// </remarks>
    private static readonly TimeSpan ParseBudget = TimeSpan.FromSeconds(2);

    /// <summary>Fixed, so a failure reproduces rather than haunting one run in ten.</summary>
    private const int Seed = 0x0DEFACED;

    private const int CasesPerTarget = 2_000;

    /// <summary>Shapes that are known to be awkward, kept as seeds to mutate around.</summary>
    private static readonly string[] Corpus =
    [
        "",
        " ",
        "\0",
        "\u001b",
        "\u001b[31m",
        "\n",
        "\r\n",
        "\\",
        "\\\\",
        "\\e",
        "\\e[31m",
        "\\",
        "\"",
        "\"unterminated",
        "'",
        "3.2a",
        "3.7b",
        "next-3.8",
        "master",
        "3.",
        ".3",
        "3.2a-rc1",
        "99999999999999999999.0",
        "-1",
        "3.2a\0trailing",
        "@1",
        "%0",
        "$0",
        "window_name value",
        "option-name value",
        "option-name",
        "option-name \"quoted value\"",
        "option-name value with spaces",
        "@user-option value",
        "status-format[0] something",
        new string('a', 4096),
        new string('\\', 64),
        "\0\u001b",
        "😀",
        "�",
    ];

    [Fact]
    public void Version_parsing_survives_anything_a_tmux_might_print()
    {
        Fuzz(nameof(TmuxVersion), input =>
        {
            // TryParse is the total function and must never throw at all.
            bool parsed = TmuxVersion.TryParse(input, out TmuxVersion result);
            if (parsed)
            {
                // A version that parsed has to be usable: comparison and
                // rendering are what the capability model does with it.
                _ = result.ToString();
                _ = result.CompareTo(result);
                Assert.Equal(0, result.CompareTo(result));
            }
        });
    }

    [Fact]
    public void Option_values_survive_broken_escapes()
    {
        Fuzz("OptionParser.ParseValue", input =>
        {
            TmuxOptionValue value = OptionParser.ParseValue(input);
            _ = value.ToString();
        });
    }

    [Fact]
    public void Option_rows_survive_truncation_and_stray_separators()
    {
        Fuzz("OptionParser.ParseSparse", input =>
        {
            // A row list is what `show-options` hands back, one line each. The
            // interesting inputs are the ones with no space, only a space, or a
            // trailing backslash.
            IReadOnlyList<TmuxOption> options = OptionParser.ParseSparse([input]);
            foreach (TmuxOption option in options)
            {
                Assert.NotNull(option.Name);
            }
        });
    }

    [Fact]
    public void Escape_decoding_never_runs_off_the_end()
    {
        // A backslash as the final character is the classic off-by-one: the
        // decoder looks at the next character and there isn't one.
        Fuzz("OptionParser.DecodeEscapes", input =>
        {
            string decoded = OptionParser.DecodeEscapes(input);
            Assert.NotNull(decoded);
        });
    }

    [Fact]
    public void Every_corpus_seed_is_handled_by_every_parser()
    {
        // The generator mutates around the corpus, so it is worth asserting the
        // corpus itself is covered rather than assuming a mutation reached it.
        foreach (string seed in Corpus)
        {
            Guarded(seed, () => Assert.IsType<bool>(TmuxVersion.TryParse(seed, out _)));
            Guarded(seed, () => OptionParser.ParseValue(seed));
            Guarded(seed, () => OptionParser.ParseSparse([seed]));
            Guarded(seed, () => OptionParser.DecodeEscapes(seed));
        }
    }

    private static void Fuzz(string target, Action<string> parse)
    {
        Random random = new(Seed);
        for (int index = 0; index < CasesPerTarget; index++)
        {
            string input = Mutate(random);
            Guarded($"{target} case {index}: {Describe(input)}", () => parse(input));
        }
    }

    /// <summary>Runs one case and reports anything that is not a clean return or a library failure.</summary>
    private static void Guarded(string description, Action body)
    {
        Stopwatch clock = Stopwatch.StartNew();
        try
        {
            body();
        }
        catch (LibTmuxException)
        {
            // A parser refusing input is a correct outcome. The library's own
            // exception type is how it says so.
        }
        catch (FormatException)
        {
            // Parse on a value type is documented to throw this; TryParse is
            // the one that must not.
        }
        catch (ArgumentException)
        {
            // Rejecting an argument is a refusal, not a crash.
        }
        catch (Exception error)
        {
            Assert.Fail(
                $"{description} threw {error.GetType().FullName}, which callers cannot be expected to catch: {error.Message}");
        }

        Assert.True(
            clock.Elapsed < ParseBudget,
            $"{description} took {clock.Elapsed}, which is long enough to be an unbounded loop.");
    }

    /// <summary>Builds an input by damaging a corpus seed in one of a few ways.</summary>
    private static string Mutate(Random random)
    {
        string seed = Corpus[random.Next(Corpus.Length)];
        return random.Next(6) switch
        {
            0 => seed,
            1 => Truncate(seed, random),
            2 => seed + Corpus[random.Next(Corpus.Length)],
            3 => InsertByte(seed, random),
            4 => new string(seed.Reverse().ToArray()),
            _ => RepeatUntilLong(seed, random),
        };
    }

    private static string Truncate(string seed, Random random) =>
        seed.Length == 0 ? seed : seed[..random.Next(seed.Length)];

    private static string InsertByte(string seed, Random random)
    {
        StringBuilder builder = new(seed);
        // Control characters and the separators this library uses are the bytes
        // most likely to be mishandled, so they are over-represented.
        char injected = random.Next(3) switch
        {
            0 => (char)random.Next(0, 0x20),
            1 => "\\\"' \t\n\r=[]".AsSpan()[random.Next(10)],
            _ => (char)random.Next(0, 0x110000 - 0x800),
        };
        builder.Insert(seed.Length == 0 ? 0 : random.Next(seed.Length), injected);
        return builder.ToString();
    }

    private static string RepeatUntilLong(string seed, Random random)
    {
        if (seed.Length == 0)
        {
            return seed;
        }

        int repeats = Math.Max(1, random.Next(64 / Math.Max(1, seed.Length)) + 1);
        return string.Concat(Enumerable.Repeat(seed, repeats));
    }

    /// <summary>Renders an input so a failure message can be pasted back into a test.</summary>
    private static string Describe(string input) =>
        input.Length > 64
            ? $"<{input.Length} chars starting {Escape(input[..32])}>"
            : Escape(input);

    private static string Escape(string input)
    {
        StringBuilder builder = new(input.Length + 2);
        builder.Append('"');
        foreach (char character in input)
        {
            builder.Append(char.IsControl(character)
                ? $"\\u{(int)character:x4}"
                : character.ToString());
        }

        return builder.Append('"').ToString();
    }
}
