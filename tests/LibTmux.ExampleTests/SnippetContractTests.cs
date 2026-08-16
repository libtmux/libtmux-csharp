using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using LibTmux.Examples;

namespace LibTmux.ExampleTests;

/// <summary>Holds the published blocks to the code that actually runs.</summary>
/// <remarks>
/// A document quotes a <c>#region</c>, and a region is only worth quoting if
/// something runs it. The name is the whole contract: a region is named after
/// the example method it sits in, so renaming one without the other is a
/// failing test rather than a document quoting code nobody executes.
/// </remarks>
[UnsupportedOSPlatform("windows")]
public sealed class SnippetContractTests
{
    private static readonly Regex Region = new(
        @"^\s*#region\s+(?<name>\S+)\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    [Fact]
    public void Every_published_region_is_an_example_that_runs()
    {
        HashSet<string> examples =
        [
            .. ExampleCase.Discover().Select(example => example.Id),
        ];

        List<string> orphans = [];
        foreach (string file in SnippetFiles())
        {
            foreach (Match match in Region.Matches(File.ReadAllText(file)))
            {
                string name = match.Groups["name"].Value;
                if (!examples.Contains(name))
                {
                    orphans.Add($"{Path.GetFileName(file)}: #region {name}");
                }
            }
        }

        Assert.True(
            orphans.Count == 0,
            "These regions are published but no [Example] method runs them:\n  "
            + string.Join("\n  ", orphans));
    }

    [Fact]
    public void Every_example_lives_where_the_snippet_reader_looks()
    {
        // The reader that materializes documents globs one directory. An
        // example outside it would run and never be publishable, which is a
        // surprise worth failing on rather than discovering in review.
        IReadOnlyList<string> files = SnippetFiles();
        Assert.NotEmpty(files);

        HashSet<string> topics =
        [
            .. files.Select(file => Path.GetFileNameWithoutExtension(file)),
        ];
        foreach (ExampleCase example in ExampleCase.Discover())
        {
            Assert.Contains(example.Topic, topics);
        }
    }

    private static IReadOnlyList<string> SnippetFiles() =>
        [.. Directory.EnumerateFiles(
            Path.Combine(RepositoryRoot(), "examples", "LibTmux.Examples", "Snippets"),
            "*.cs")];

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LibTmux.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The repository root was not found.");
    }
}
