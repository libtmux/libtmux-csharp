# LibTmux

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
throws. It never quietly falls back to filtering in memory:

```csharp
using LibTmux.Query;

QueryDocument document = QueryExtensions.Translate<Session>(
    session => session.Name.StartsWith("build") && session.IsAttached);
```

The same expression compiles to a predicate for matching what you already hold.

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

## Optional packages

`LibTmux.Query.Json` adds `System.Text.Json` support for query documents. The
core library does not reference it, so a caller who does not want a JSON
dependency does not get one.

## License

MIT.
