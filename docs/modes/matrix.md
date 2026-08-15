# Choosing a mode

Three ways to reach tmux. Which one you are using is visible where the call
starts, never in a flag buried in options, and all three are supported on every
tmux this library supports.

| Mode | Flip it on | Dispatch | Example output |
|---|---|---|---|
| [One-shot](one-shot.md) | `session.CreateWindowAsync(...)` | 1 command, awaited | `@1 1:build` |
| [Control](control-mode.md) | `server.EnterControlModeAsync(ct)` | 1 client, streamed | `%window-add @1` |
| [Chained](chaining.md) | `server.Chain()...ExecuteAsync(ct)` | N batched, 1 invocation | `@1` |

Example output is captured from a live tmux and refreshed per release. It shows
the shape of what comes back, not a byte-exact string to assert against.

## The same task, three ways

Create a window named `build`.

```csharp
Window window = await session.CreateWindowAsync(new NewWindowRequest(name: "build"), ct);
```

```csharp
await using IControlModeSession control = await server.EnterControlModeAsync(cancellationToken: ct);
await control.SendAsync("new-window -d -n build", ct);
```

```csharp
await server.Chain().Then("new-window", "-d", "-n", "build").ExecuteAsync(ct);
```

## What each costs

One-shot starts a tmux client per command. Chaining starts one for the whole
sequence. Control mode starts one and keeps it, so commands after the first pay
no process cost at all, in exchange for holding a connection.

Measured by the `LibTmux.Benchmarks` project against tmux 3.7b on `net10.0`,
one idle machine:

| Commands | Mode | Mean | Allocated |
|---|---|---:|---:|
| 1 | One-shot | 8.1 ms | 287 KB |
| 1 | Chained | 9.0 ms | 288 KB |
| 1 | Control | 0.09 ms | 1 KB |
| 50 | One-shot | 344 ms | 14,337 KB |
| 50 | Chained | 9.5 ms | 363 KB |
| 50 | Control | 7.9 ms | 59 KB |

Read the crossover, not the absolute numbers, which depend on the machine.
**Chaining is slightly slower than one-shot for a single command** — you pay to
build a chain and gain nothing, because there is only one process either way.
It wins the moment there is more than one command, and by fifty it is roughly
thirty times faster and allocates forty times less. Control mode beats both
once its client is already running, which is what makes it worth holding.

The allocation column is the part that does not move: it is the same on a busy
machine as an idle one, so it is what to check a change against. Timings need
enough iterations to separate the modes — at a handful, the error exceeds the
mean and the single-command comparison inverts, which is measuring the machine
rather than the library.

Run it for your own tmux and machine:

```console
$ dotnet run \
    --project benchmarks/LibTmux.Benchmarks \
    --configuration Release \
    -- --filter '*ModeBenchmarks*' \
    --warmupCount 5 \
    --iterationCount 15
```

## Version differences

Behavior that differs across tmux 3.2a to 3.7b goes through the capability
model, and each difference has a row with a real-server proof in
[the parity ledger](../parity/version-deltas.json).
