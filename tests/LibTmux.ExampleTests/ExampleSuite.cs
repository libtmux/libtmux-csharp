using System.Runtime.Versioning;
using LibTmux.Examples;

namespace LibTmux.ExampleTests;

/// <summary>Examples share one process environment, so they run one at a time.</summary>
/// <remarks>
/// What makes a published example readable is that it names no socket: it
/// connects the way a reader would, and the ambient environment decides where
/// that lands. An ambient environment is process-wide, so two examples running
/// at once would be two examples in one world.
/// </remarks>
[CollectionDefinition("Examples", DisableParallelization = true)]
public sealed class OneExampleAtATime;

/// <summary>Runs every documented example against live tmux, one test each.</summary>
[Collection("Examples")]
[UnsupportedOSPlatform("windows")]
public sealed class ExampleSuite
{
    public static TheoryData<string> Examples =>
    [
        .. ExampleCase.Discover().Select(example => $"{example.Topic}.{example.Id}"),
    ];

    [Theory]
    [MemberData(nameof(Examples))]
    public async Task Example_runs_against_live_tmux(string name)
    {
        ExampleCase example = Find(name);
        await example.RunAsync(TestContext.Current.CancellationToken);
    }

    // A suite that discovered nothing passes every test it has, which is the
    // failure mode this exists to catch.
    [Fact]
    public void At_least_one_example_is_published() => Assert.NotEmpty(ExampleCase.Discover());

    private static ExampleCase Find(string name) =>
        ExampleCase.Discover().Single(
            example => string.Equals($"{example.Topic}.{example.Id}", name, StringComparison.Ordinal));
}
