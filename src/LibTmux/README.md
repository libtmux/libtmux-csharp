# LibTmux

A typed, async-first [tmux](https://github.com/tmux/tmux) client for .NET.
Servers, sessions, windows, panes, clients, options, hooks and buffers, against
every tmux from **3.2a to 3.7b**, on **net8.0** and **net10.0**.

> **Alpha.** The public API is not settled and can change between prereleases
> without notice, so pin an exact version. The behaviour is proven against all
> seven supported tmux versions on every commit.

```console
$ dotnet add package LibTmux --prerelease
```

One dependency: `Microsoft.Extensions.Logging.Abstractions`, which is
interfaces with no implementation attached — a caller who wants no logging pays
nothing for it.

## Start here

```csharp
using LibTmux;

Server server = await Server.ConnectAsync();
Session session = await server.CreateSessionAsync(new NewSessionRequest(name: "build"));
Window window = await session.CreateWindowAsync(new NewWindowRequest(name: "tests"));
Pane pane = (await window.GetPanesAsync())[0];

await pane.SendTextAsync("dotnet test");
await pane.EnterAsync();
```

`ConnectAsync` with no arguments finds the tmux a `TMUX` variable names, or the
default socket. To reach one server in particular:

```csharp
Server elsewhere = await Server.ConnectAsync(
    new ServerConnectionOptions(socketName: "build-box"));
```

Every call that reaches tmux is asynchronous and takes a `CancellationToken`.
There are no synchronous twins to choose between.

## Three ways to reach tmux

Which one a call uses is visible where the call starts, and all three work on
every supported tmux.

| Mode | Flip it on | Dispatch | 1 command | 50 commands |
|---|---|---|---:|---:|
| One-shot | `session.CreateWindowAsync(…)` | one command, awaited | 3.8 ms | 118 ms |
| Control | `server.EnterControlModeAsync(ct)` | one client, streamed | 0.29 ms | 6.5 ms |
| Chained | `server.Chain()…ExecuteAsync(ct)` | N batched, one invocation | 3.6 ms | 3.5 ms |

Chaining's two cells are one measurement, not two: 3.6 ms ± 1.1 and 3.5 ms ±
0.6 overlap completely. A chain pays for one tmux process and almost nothing
per command after it — 312 KB allocated for one command, 388 KB for fifty — so
fifty commands cost what one costs. That is the reason to reach for it.

```csharp run
// One command, a typed object back.
Window built = await session.CreateWindowAsync(new NewWindowRequest(name: "build"), ct);
```

```csharp run
// One client, held open, streaming what tmux does on its own.
await using IControlModeSession control = await server.EnterControlModeAsync(cancellationToken: ct);
IReadOnlyList<string> reply = await control.SendAsync("list-windows", ct);
```

```csharp run
// Many commands, one invocation, one process cost.
await server.Chain()
    .Then("new-window", "-d", "-n", "one")
    .Then("new-window", "-d", "-n", "two")
    .ExecuteAsync(ct);
```

Control mode is an order of magnitude cheaper *per command*; a chain wins *for
a batch* by paying one round trip for the whole sequence.

## Reading what is there

Accessors return `IReadOnlyList<T>` over an explicit read and never shell out
while you enumerate them:

```csharp run
foreach (Window each in await session.GetWindowsAsync(ct))
{
    foreach (Pane every in await each.GetPanesAsync(ct))
    {
        Console.WriteLine($"{each.Name} {every.Index} {every.Width}x{every.Height}");
    }
}
```

A handle says what it read, and that stays true. Operations that change what an
object is hand back a replacement:

```csharp run
Window renamed = await window.RenameAsync("integration", ct);
```

Asking tmux again is `RefreshAsync`. A whole hierarchy in one read is
`CaptureSnapshotAsync`:

```csharp run
Server snapshot = await server.CaptureSnapshotAsync(SnapshotDepth.Panes, ct);
```

## Running something, and reading it back

```csharp run
await pane.SendTextAsync("echo hello-from-libtmux", cancellationToken: ct);
await pane.EnterAsync(ct);

// tmux accepts a command before the shell has finished it, so the result is
// waited for rather than assumed.
string output = await TmuxWait.UntilAsync(
    async token => string.Join('\n', await pane.CaptureAsync(cancellationToken: token)),
    text => text.Contains("hello-from-libtmux", StringComparison.Ordinal),
    TimeSpan.FromSeconds(10),
    TimeSpan.FromMilliseconds(20));
```

## Splitting and resizing

```csharp run
Pane split = await pane.SplitAsync(new SplitPaneRequest(direction: PaneDirection.Below), ct);
await split.SetHeightAsync(10, ct);
```

## Options and hooks

tmux has no types, so a value carries the text it reported alongside the
readings that text supports:

```csharp run
await window.Options.SetAsync(new SetOptionRequest("automatic-rename", "off"), ct);
TmuxOption option = (await window.Options.GetAsync(
    new GetOptionRequest("automatic-rename"), ct))[0];

Console.WriteLine($"{option.Value.Raw} flag={option.Value.Boolean}");
```

An option the window does not hold is inherited rather than missing. Hooks are
arrays even with one entry:

```csharp run
TmuxHook hook = await server.Hooks.SetAsync(
    new SetHookRequest("alert-bell", "set-option -g @rang yes"), ct);
```

## Filtering

Ordinary filtering is LINQ over what you read:

```csharp run
IReadOnlyList<Window> windows = await session.GetWindowsAsync(ct);
IEnumerable<Window> building = windows.Where(
    each => each.Name.StartsWith("build", StringComparison.Ordinal));
```

Declarative filtering translates an expression into a portable document, or
throws — it never quietly falls back to filtering in memory. Write it over the
objects you already hold:

```csharp run
IReadOnlyList<Session> sessions = await server.GetSessionsAsync(ct);
IReadOnlyList<Session> building = sessions.Matching<Session>(
    session => session.Name.StartsWith("build") && session.Attached);
```

Relations quantify, and the element type carries its own fields:

```csharp run
Server captured = await server.CaptureSnapshotAsync(SnapshotDepth.Windows, ct);
IReadOnlyList<Session> withBuild = captured.Sessions.Matching<Session>(
    session => session.Windows.Any(each => each.Name.StartsWith("build")));
```

The same expression is also a document, which can be written here and answered
somewhere else:

```csharp run
QueryDocument document = QueryExtensions.Translate<Session>(
    session => session.Name.StartsWith("build") && session.Attached);
```

You write C# and tmux receives tmux. The catalog carries the pair for all
twelve queryable fields — `Session.Name` is `session_name`,
`Client.IsControlClient` is `client_control` — and it is closed:

| Session | Window | Pane | Client |
|---|---|---|---|
| `Name`, `Id`, `Attached`, `Windows` | `Name`, `Id`, `Panes` | `Id`, `pane_command` | `Name`, `IsControlClient`, `client_id` |

Two fields have no property on their entity, and are reached by declaring a row
whose property names are the wire names — which is also how you query a
projection rather than an entity:

```csharp
internal sealed record PaneRow(string PaneId, string PaneCommand);
```

A field outside the catalog throws `UnsupportedQueryExpressionException` rather
than falling back, so an expression that translates is one tmux can answer.

Put it on the wire with
[LibTmux.Query.Json](https://www.nuget.org/packages/LibTmux.Query.Json).

## Versions

Where a flag is missing on the running tmux, the request goes out without it
and a warning says what was left off. Where a whole command is missing, nothing
is sent and `TmuxVersionTooLowException` says which version would be needed.

```csharp run
// A handle says what it read: the version is what tmux reported when this
// server was reached, and null when it reported something unparsable.
TmuxVersion? version = server.Version;
Console.WriteLine($"tmux {version?.Raw} 3.4-or-newer={version?.IsAtLeast(TmuxVersion.Parse("3.4"))}");
```

## Testing your own code

`LibTmux.Testing` ships in this package. It gives a test a tmux server of its
own, on its own socket, killed deterministically:

```csharp
using LibTmux.Testing;

TmuxTestFactory factory = new();
await using TemporaryHierarchyScope scope = await factory.CreateHierarchyAsync();

await scope.Pane.SendTextAsync("echo hello");
await scope.Pane.EnterAsync();
```

Disposing kills the server, so a test that fails part way through leaves
nothing behind.

## Logging

Pass an `ILogger` when connecting and every tmux command is recorded once, at
the single point they all pass through:

```csharp
Server logged = await Server.ConnectAsync(new ServerConnectionOptions(logger: logger));
```

Commands are recorded at `Debug` and failures at `Error`, with stable scalar
fields (`TmuxSubcommand`, `TmuxExitCode`) to filter on. Anything that can carry
a payload is truncated, the command line included.

## Related packages

| Package | Adds |
|---|---|
| [LibTmux.Query.Json](https://www.nuget.org/packages/LibTmux.Query.Json) | JSON for query documents |
| [LibTmux.Workspace](https://www.nuget.org/packages/LibTmux.Workspace) | Sessions from tmuxp YAML |
| [LibTmux.Mcp](https://www.nuget.org/packages/LibTmux.Mcp) | A Model Context Protocol server, as a .NET tool |

Source, docs and issues: <https://github.com/libtmux/libtmux-csharp>

## License

[MIT](https://github.com/libtmux/libtmux-csharp/blob/master/LICENSE)
