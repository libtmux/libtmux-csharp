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

Measured by the `LibTmux.Benchmarks` project against tmux 3.7b on `net10.0`:

| Commands | Mode | Mean | Error | Allocated |
|---|---|---:|---:|---:|
| 1 | One-shot | 3.8 ms | ± 0.8 | 312 KB |
| 1 | Chained | 3.6 ms | ± 1.1 | 313 KB |
| 1 | Control | 0.29 ms | ± 0.05 | 1.3 KB |
| 50 | One-shot | 118 ms | ± 8.1 | 15,576 KB |
| 50 | Chained | 3.5 ms | ± 0.6 | 388 KB |
| 50 | Control | 6.5 ms | ± 0.8 | 59 KB |

The error column is there because one comparison in this table needs it:
chaining one command and chaining fifty are **the same measurement**. 3.6 ± 1.1
and 3.5 ± 0.6 overlap completely, and reading the means alone would say fifty
commands are faster than one, which is not a thing. The allocations say what is
actually happening — 313 KB for one, 388 KB for fifty — a chain pays for one
tmux process and almost nothing per command after it.

There are two crossovers here and they point at different modes.

**Per command, control mode wins by an order of magnitude** — 0.29 ms against
3.6, and 1.3 KB against 312 — because its client is already running and nothing
starts a process. That is what makes holding a connection worth it.

**For a batch handed over at once, chaining wins** — fifty commands in one
invocation cost 3.5 ms, less than fifty round trips on a connection that is
already open. A chain pays for one round trip however many commands are in it;
control mode pays for fifty.

One-shot is the mode that does not scale: fifty commands is fifty processes,
118 ms and 15 MB of it. At a single command it is indistinguishable from a
chain, and the allocations say why — 312 KB either way, because both start
exactly one process.

The allocation column is the part that does not move: it repeated to two
decimal places across runs here while the means moved by a factor of two, so it
is what to check a change against. Timings depend on the machine and on what
else it is doing, which is why this table is worth rerunning rather than
believing.

Run it for your own tmux and machine:

```console
$ dotnet run \
    --project benchmarks/LibTmux.Benchmarks \
    --configuration Release \
    --framework net10.0 \
    -- --filter '*ModeBenchmarks*' \
    --warmupCount 10 \
    --iterationCount 30
```

The project multi-targets, so the framework has to be named: the numbers above
are `net10.0`, and running the other one measures something else.

## Version differences

Behavior that differs across tmux 3.2a to 3.7b goes through the capability
model, and each difference has a row with a real-server proof in
[the parity ledger](../parity/version-deltas.json).
