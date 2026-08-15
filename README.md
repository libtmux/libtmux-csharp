# LibTmux

> **Alpha.** `0.0.0-alpha.1` is the first prerelease. The public API is not
> settled and can change between prereleases without notice, so pin an exact
> version rather than a range. Everything published here is tested against
> tmux 3.2a through 3.7b on every commit — what is unsettled is the shape of
> the API, not whether it works.

A typed, async-first client for [tmux](https://github.com/tmux/tmux). Drive
servers, sessions, windows, panes, clients, options, hooks, and buffers from
.NET, against the same tmux versions Python
[libtmux](https://github.com/tmux-python/libtmux) supports: 3.2a through 3.7b.

```csharp
using LibTmux;

Server server = await Server.ConnectAsync();
Session session = await server.CreateSessionAsync(new NewSessionRequest(name: "build"));
Window window = await session.CreateWindowAsync(new NewWindowRequest(name: "tests"));
Pane pane = (await window.GetPanesAsync())[0];

await pane.SendTextAsync("dotnet test");
await pane.EnterAsync();
```

## Installing

```console
$ dotnet add package LibTmux
```

The package targets `net8.0` and `net10.0`, is trim- and AOT-safe, and takes one
dependency: `Microsoft.Extensions.Logging.Abstractions`.

## Three ways to reach tmux

Which one a call uses is visible where the call starts, never a flag buried in
options, and all three work on every tmux this library supports.

| Mode | Flip it on | Dispatch | Example output | 1 command | 50 commands |
|---|---|---|---|---:|---:|
| [One-shot](docs/modes/one-shot.md) | `session.CreateWindowAsync(...)` | 1 command, awaited | `@1 1:build` | 3.8 ms | 118 ms |
| [Control](docs/modes/control-mode.md) | `server.EnterControlModeAsync(ct)` | 1 client, streamed | `%window-add @1` | 0.29 ms | 6.5 ms |
| [Chained](docs/modes/chaining.md) | `server.Chain()...ExecuteAsync(ct)` | N batched, 1 invocation | `@1` | 3.6 ms | 3.5 ms |

The same window, three ways:

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

One-shot starts a tmux client per command, chaining starts one for the whole
sequence, and control mode starts one and keeps it. Read the crossovers rather
than the numbers, which depend on the machine: control mode is an order of
magnitude cheaper *per command* because its client is already running, while a
chain beats it *for a batch* by paying one round trip for the whole sequence.
[Choosing a mode](docs/modes/matrix.md) has allocations, both crossovers, and
how to rerun the table on your own machine.

## What the API looks like

Every call that reaches tmux is asynchronous and takes a `CancellationToken`.
There are no synchronous twins to choose between.

Operations that change what an object is return a replacement rather than
mutating the handle you hold:

```csharp
Window renamed = await window.RenameAsync("integration");
```

The handle you started with keeps saying what it read, which stays true. Asking
tmux again is `RefreshAsync`.

### Reading state

Accessors return `IReadOnlyList<T>` and never shell out while you enumerate
them, so a `foreach` cannot surprise you with a tmux command per item:

```csharp
foreach (Session existing in await server.GetSessionsAsync())
{
    Console.WriteLine(existing.Name);
}
```

### Filtering

Ordinary filtering is LINQ:

```csharp
IEnumerable<Window> named = (await session.GetWindowsAsync())
    .Where(window => window.Name.StartsWith("test", StringComparison.Ordinal));
```

Declarative filtering translates an expression to a portable document, or
throws. It never quietly falls back to filtering in memory.

The expression is written over a row you declare, whose property names are the
tmux fields it reads. That is what lets the same expression become a document
tmux can answer rather than a predicate only this process understands:

```csharp
internal sealed record SessionRow(string SessionName, bool SessionAttached);
```

```csharp
using LibTmux.Query;

QueryDocument document = QueryExtensions.Translate<SessionRow>(
    row => row.SessionName.StartsWith("build") && row.SessionAttached);
```

A field outside the catalog throws `UnsupportedQueryExpressionException` rather
than falling back, so an expression that translates is one tmux can answer.

The same expression matches what you already hold, in memory:

```csharp
IReadOnlyList<SessionRow> attached = rows.Matching<SessionRow>(
    row => row.SessionName.StartsWith("build") && row.SessionAttached);
```

### Options and hooks

Each object reaches the option table tmux keeps for it:

```csharp
await window.Options.SetAsync(new SetOptionRequest("automatic-rename", "off"));
TmuxOption option = (await window.Options.GetAsync(new GetOptionRequest("automatic-rename")))[0];
```

tmux has no types, so a value carries the text it reported alongside the
readings that text supports:

```csharp
bool? flag = option.Value.Boolean;   // false
long? number = option.Value.Integer; // null
string? raw = option.Value.Raw;      // "off"
```

Hooks work the same way, and are arrays even with one entry.

### Versions

tmux grew flags over the supported range. Where a flag is missing, the request
still goes out once without it and a warning says what was left off. Where a
whole command is missing, nothing is sent and a `TmuxVersionTooLowException`
says which version would be needed.

## Testing with tmux

`LibTmux.Testing` gives tests a tmux server of their own, cleaned up
deterministically:

```csharp
using LibTmux.Testing;

TmuxTestFactory factory = new();
await using TemporaryHierarchyScope scope = await factory.CreateHierarchyAsync();

await scope.Pane.SendTextAsync("echo hello");
await scope.Pane.EnterAsync();
```

Disposing kills the server, so a test that fails part way through still leaves
nothing behind. `TmuxWait.UntilAsync` waits for a state rather than sleeping,
which is what keeps tmux tests from being timing-dependent.

## Logging

Pass an `ILogger` when connecting and every tmux command is recorded once, at
the single point they all pass through:

```csharp
Server server = await Server.ConnectAsync(new ServerConnectionOptions(logger: logger));
```

Commands are recorded at `Debug` and failures at `Error`, with stable scalar
fields (`TmuxSubcommand`, `TmuxExitCode`) to filter and group on. Everything
that can carry a payload is truncated, the command line included: setting a
buffer puts whatever was copied into the arguments.

## Documentation

[docs/](docs/README.md) covers the three modes, the rendered API reference, and
the records the library is held to: the approved public surface, every tmux
version difference with the test that proves it, and where each Python libtmux
symbol went.

## Optional packages

The core library depends on `Microsoft.Extensions.Logging.Abstractions` and
nothing else. Anything that would add a dependency ships as its own package, so
a caller who does not want one does not get it:

| Package | What it adds | Dependency |
|---|---|---|
| [LibTmux.Query.Json](src/LibTmux.Query.Json/README.md) | `System.Text.Json` support for query documents | `System.Text.Json` |
| [LibTmux.Workspace](src/LibTmux.Workspace/README.md) | Builds sessions from tmuxp workspace files | `YamlDotNet` |
| [LibTmux.Mcp](src/LibTmux.Mcp/README.md) | A Model Context Protocol server | none — it installs as a tool |

They ship from this repository and carry its version, so `LibTmux.Mcp
0.0.0-alpha.1` goes with `LibTmux 0.0.0-alpha.1` without a compatibility table
to consult.

## License

MIT.
