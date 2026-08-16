using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;

namespace LibTmux.Benchmarks;

/// <summary>Measures each mode as a distribution rather than a single number.</summary>
/// <remarks>
/// What these benchmarks time is a tmux process starting, and that cost is not
/// stable: it depends on what else the machine is doing, and it moved by a
/// factor of five between two runs on the same machine here. A mean alone hides
/// that, and two means whose intervals overlap can be read in whichever order
/// flatters the library — which is how "fifty commands are faster than one" ends
/// up in a table.
///
/// So every case is sampled <see cref="SampleCount"/> times, one operation per
/// sample, and reported across the distribution. A claim that survives the p95
/// is a claim about the library; one that only holds at the mean is a claim
/// about the afternoon.
/// </remarks>
internal sealed class ModeBenchmarkConfig : ManualConfig
{
    /// <summary>How many samples each case contributes.</summary>
    internal const int SampleCount = 100;

    /// <summary>How many samples are discarded before recording starts.</summary>
    /// <remarks>
    /// This number is high because a low one produced a false result. Cases run
    /// in order, the first one runs while the runtime is still tiering up and
    /// the tmux binary is not yet in the page cache, and five discarded samples
    /// did not outlast that. The penalty landed on whichever case went first,
    /// which made chaining one command measure slower than chaining fifty — in
    /// two independent runs, so it read as a finding rather than as the warmup
    /// artefact it was. At forty the order comes out the only way it can: fifty
    /// commands cost more than one.
    /// </remarks>
    internal const int WarmupCount = 40;

    public ModeBenchmarkConfig()
    {
        // Monitoring runs the method once per sample instead of batching it into
        // a timed loop. That is what makes a sample a real command rather than an
        // average over an unknown number of them, and it is the mode BenchmarkDotNet
        // documents for operations that are slow because they wait on something.
        AddJob(Job.Default
            .WithStrategy(RunStrategy.Monitoring)
            .WithWarmupCount(WarmupCount)
            .WithIterationCount(SampleCount)
            .WithInvocationCount(1)
            .WithUnrollFactor(1));

        AddColumn(
            StatisticColumn.Min,
            StatisticColumn.Median,
            StatisticColumn.Mean,
            StatisticColumn.StdDev,
            StatisticColumn.P90,
            StatisticColumn.P95,
            StatisticColumn.Max);

        // The full export carries every sample, which is what the recorder reads
        // to compute a p99 and to write a record that can be checked rather than
        // trusted.
        AddExporter(JsonExporter.Full);
    }
}
