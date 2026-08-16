# Benchmarks

Recorded runs, each naming the tmux, host, runtime and commit that produced it.
Nothing here is a promise about your machine.

## Runs

| Collected | tmux | Library | Record |
|---|---|---|---|
| 2026-08-16 | 3.7b | `0.0.0-alpha.3` | [record](runs/2026-08-16-tmux-3.7b.md) |

## Why a record rather than a number

A tmux command is a process start, and a process start is not a stable
quantity. The same machine, the same commit and the same tmux measured a
one-shot command at 3.3 ms and at 4.7 ms in two runs an hour apart, and at 19 ms
while a build was running. Any single millisecond figure quoted without its
conditions is quoting the conditions.

So each run records the whole distribution — min, median, mean, p90, p95, p99,
max over 100 samples — and the conditions that produced it. A claim that holds
at the p95 of a recorded run is a claim about the library. A claim that holds
only at the mean is a claim about that afternoon.

## What is comparable and what is not

**Comparable across machines:** the shape. One-shot cost grows with the number
of commands because each one starts a process. Chained cost does not, because
all of them share one. Control-mode cost grows, but by a round trip rather than
a process. Those orderings held in every run recorded here.

**Not comparable across machines:** the milliseconds, and the crossover between
chaining and control mode. Which of the two wins at fifty commands depends on
what a process start costs relative to a round trip, and that ratio is a
property of the host. Both orders have been measured here.

**Comparable exactly:** allocation. It repeated byte-for-byte across runs while
the timings moved by a factor of five, so it is what a change should be checked
against.

## Reproducing

```console
$ dotnet run \
    --project benchmarks/LibTmux.Benchmarks \
    --configuration Release \
    --framework net10.0 \
    -- --filter '*ModeBenchmarks*' --artifacts artifacts/benchmarks
```

The project multi-targets, so the framework has to be named; the recorded runs
are `net10.0`. To measure a specific tmux rather than whatever is on the path,
set `LIBTMUX_TMUX` to its binary.

Turn the result into a record:

```console
$ uv run python eng/benchmarks/record_modes.py \
    --report artifacts/benchmarks/results/LibTmux.Benchmarks.ModeBenchmarks-report-full.json \
    --tmux-version 3.7b \
    --collected 2026-08-16 \
    --out docs/benchmarks/runs
```

## A warning worth keeping

The first version of this benchmark discarded five warmup samples. Cases run in
order, and five was not enough to outlast the runtime tiering up and the tmux
binary reaching the page cache, so the first case to run absorbed the cost. That
made chaining one command measure *slower* than chaining fifty — reproducibly,
across two independent runs, which is exactly what makes an artefact convincing.
It reached the README as a table saying fifty commands were faster than one.

Forty warmup samples fixed it. The lesson is in
[`ModeBenchmarkConfig`](../../benchmarks/LibTmux.Benchmarks/ModeBenchmarkConfig.cs):
a result that cannot be true is not a finding, however many times it reproduces.
