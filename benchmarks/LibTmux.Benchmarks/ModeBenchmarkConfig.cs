using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;

namespace LibTmux.Benchmarks;

/// <summary>Measures each mode as a distribution rather than a single number.</summary>
/// <remarks>
/// tmux's process-start cost varies by machine load — by a factor of five
/// between runs seen here — so each case is sampled <see cref="SampleCount"/>
/// times and reported by percentile (e.g. p95) rather than by mean.
/// </remarks>
internal sealed class ModeBenchmarkConfig : ManualConfig
{
    /// <summary>How many samples each case contributes.</summary>
    internal const int SampleCount = 100;

    /// <summary>How many samples are discarded before recording starts.</summary>
    /// <remarks>
    /// High enough to outlast the runtime's JIT tiering and the tmux binary's
    /// first page-cache load; a lower count let those startup costs bias
    /// whichever case ran first.
    /// </remarks>
    internal const int WarmupCount = 40;

    public ModeBenchmarkConfig()
    {
        // Monitoring times the method once per sample instead of a batched loop —
        // BenchmarkDotNet's strategy for operations slow because they wait on something.
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

        // Full export keeps every sample, so a recorded run's p99 can be
        // computed and checked rather than trusted.
        AddExporter(JsonExporter.Full);
    }
}
