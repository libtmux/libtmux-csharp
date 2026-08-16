# LibTmux.Mcp

A [Model Context Protocol](https://modelcontextprotocol.io) server that gives
an assistant hands on tmux, built on
[LibTmux](https://www.nuget.org/packages/LibTmux).

> **Alpha.** The tool surface is not settled and can change between prereleases
> without notice, so pin an exact version.

This is a **.NET tool**, not a library reference:

```console
$ dotnet tool install --global LibTmux.Mcp --prerelease
```

## Point a client at it

It speaks the protocol over standard input and output, which is how an MCP
client starts it:

```json
{
  "mcpServers": {
    "tmux": {
      "command": "libtmux-mcp"
    }
  }
}
```

To drive a server other than the ambient one — a sandbox, a test rig, a
long-lived project socket — pass a socket name:

```json
{
  "mcpServers": {
    "tmux": {
      "command": "libtmux-mcp",
      "args": ["my-socket"]
    }
  }
}
```

## What it is for

Anything where the answer lives in a terminal rather than in a file. Run a
build and learn whether it passed. Watch a dev server come up. Find which of
eleven panes is showing the stack trace. Lay out a workspace and drive it.

The design goal is that an assistant never gets **stuck** and never **wastes
context**: no tool polls, no tool returns unbounded output, and no failure
comes back as "an error occurred".

## Waiting, not polling

The tool an assistant reaches for first is usually the wrong one. These four
cover the cases, and the server's instructions steer between them:

| You want | Use | Why |
|---|---|---|
| Run a command, know if it worked | `tmux_run` | Waits, returns the shell's **real exit status** |
| The same, but it takes minutes | `tmux_start_job` → `tmux_job` | Returns a handle at once; collect later |
| Output you did **not** start | `tmux_wait_for_text` | Wakes on the pane printing, not on a timer |
| Watch a pane across turns | `tmux_tail_pane` | Answers only what is **new** since its cursor |

A client that speaks the [Tasks extension](https://modelcontextprotocol.io) can
start `tmux_run`, `tmux_wait_for_text`, `tmux_wait_for_channel` or `tmux_job`
as a task and collect the result later — the protocol's own version of what
`tmux_start_job` does by hand. It is offered, never required, so a client
without it keeps the blocking call it had. A listing stays a plain call: making
it a task would cost a round trip to collect an answer that was already there.

Nothing here sleeps in a loop. A wait subscribes to tmux's own
[control mode](https://github.com/tmux/tmux/wiki/Control-Mode), so tmux reports
pane output as it happens and the wait is released the moment there is
something to look at.

Two details make that safe. The control client attaches with `ignore-size`
(tmux 3.2+), so it never drags the window down to its own size; and it is
reference counted per session, so it exists only while a wait is running. What
arrives on that stream is the pane's raw terminal bytes, so it is used as a
signal and never as content — the text you get always comes from a capture,
which is what tmux has already rendered.

If control mode cannot start, waits fall back to polling. Cost changes;
answers do not.

The tools are ordinary classes, so an application that already hosts an
assistant can run one directly instead of launching a second process:

<!-- snippet: RunAndReadExitStatus usings: LibTmux, LibTmux.Mcp -->
```csharp
using LibTmux;
using LibTmux.Mcp;

WriteTools tools = McpTools.Writing(server.ConnectionOptions);

RunResult result = await tools.RunAsync(
    "test -f /etc/hostname && echo present",
    pane.Id.ToString(),
    timeoutSeconds: 20,
    cancellationToken: ct);

// The status comes from the shell, not from reading the screen, so a
// command that prints nothing still says what it did.
Console.WriteLine($"exit {result.ExitStatus}, timed out: {result.TimedOut}");
```
<!-- endsnippet -->

## Nothing returns unbounded output

Every content-bearing result is cut to a budget, keeps the **newest** lines,
and says what it dropped:

```json
{
  "lines": ["...", "make: *** [build] Error 1"],
  "truncated": true,
  "droppedLines": 407,
  "droppedBytes": 2034
}
```

A reader that cannot see lines are missing concludes the pane never printed
them:

<!-- snippet: KeepTheNewestLines usings: LibTmux, LibTmux.Mcp -->
```csharp
using LibTmux;
using LibTmux.Mcp;

ReadTools reading = McpTools.Reading(server.ConnectionOptions);

CaptureResult captured = await reading.CapturePaneAsync(
    pane.Id.ToString(),
    includeHistory: true,
    maxLines: 5,
    cancellationToken: ct);

// A terminal's newest line is the one that says what happened, so the
// budget keeps the end — and says what it cost, because a reader who
// cannot see the loss concludes the pane never printed it.
Console.WriteLine(captured.Content.ToDisplayString());
Console.WriteLine($"dropped {captured.Content.DroppedLines} earlier lines");
```
<!-- endsnippet -->

`tmux_tail_pane` avoids the problem instead of managing it. Pass its cursor
back and the tenth read of a busy pane costs what the first did:

<!-- snippet: ReadOnlyWhatIsNew usings: LibTmux, LibTmux.Mcp -->
```csharp
using LibTmux;
using LibTmux.Mcp;

ReadTools reading = McpTools.Reading(server.ConnectionOptions);
string paneId = pane.Id.ToString();

// A first call establishes a position and returns nothing, so watching
// a pane never starts by paying for a screenful nobody asked for.
TailResult first = await reading.TailPaneAsync(paneId, cancellationToken: ct);

await reading.WaitForTextAsync(
    paneId,
    patterns: null,
    timeoutSeconds: 5,
    cancellationToken: ct);

TailResult next = await reading.TailPaneAsync(paneId, first.Cursor, cancellationToken: ct);
Console.WriteLine($"{next.Content.Lines.Count} new lines");
```
<!-- endsnippet -->

To offer these beside your own tools rather than as a separate process:

<!-- snippet: HostTheToolsYourself usings: LibTmux, LibTmux.Mcp, Microsoft.Extensions.DependencyInjection -->
```csharp
using LibTmux;
using LibTmux.Mcp;
using Microsoft.Extensions.DependencyInjection;

ServiceCollection services = new();
services.AddLogging();

// Registers the tools, resources and prompts, and gates them on the
// tier. Choose the transport yourself — this returns the builder.
McpServerComposition.Add(
    services,
    new ServerPolicy { Tier = SafetyTier.ReadOnly },
    server.ConnectionOptions,
    callerPaneId: null);
```
<!-- endsnippet -->

## Three tiers, and a tool you do not have cannot be called

`LIBTMUX_SAFETY` picks how much of tmux is exposed. Tools above the tier are
**not registered**, so they never reach the model's list:

| `LIBTMUX_SAFETY` | Offers | Example |
|---|---|---|
| `readonly` | Reading only | `tmux_capture_pane`, `tmux_search_panes` |
| `mutating` *(default)* | Reading, creating, changing | `tmux_run`, `tmux_split_pane` |
| `destructive` | Everything, including removal | `tmux_kill_session` |

A tier bounds the tools, not the intent: an assistant denied `tmux_kill_session`
can still type `exit` into a pane with `tmux_send_keys`. Use `readonly` when
that distinction matters.

## Configuration

| Variable | Default | Does |
|---|---|---|
| `LIBTMUX_SAFETY` | `mutating` | Which tier to register |
| `LIBTMUX_SOCKET` | *(ambient)* | Socket used when a call names none |
| `LIBTMUX_TMUX` | `tmux` | Which tmux binary to drive |
| `LIBTMUX_MCP_WAIT_MAX_SECONDS` | `30` | Ceiling on any one wait |
| `LIBTMUX_MCP_MAX_LINES` | `500` | Default line budget |
| `LIBTMUX_MCP_MAX_BYTES` | `128000` | Byte budget per result |

An unreadable value is clamped and logged rather than refused — except
`LIBTMUX_SAFETY`, where anything unrecognised falls to `readonly`, because a
typo must never widen what the server offers.

## Resources and prompts

Six resources expose the hierarchy without a tool call — `tmux://hierarchy`,
`tmux://sessions`, `tmux://sessions/{id}/panes`, `tmux://panes/{id}/content`,
`tmux://self`, `tmux://servers`. A client can pin or refresh one on its own
initiative; one nobody reads costs nothing.

A client that subscribes to `tmux://hierarchy`, `tmux://sessions` or
`tmux://servers` is told when they change, from tmux's own notifications rather
than from a timer — so a view goes stale only when something actually moved.
That watcher holds a second control client, started on the first subscription
and stopped with the last, attached with `no-output` because it wants the
hierarchy and not every byte a pane prints.

Both subscription shapes are served: `resources/subscribe`, and the
`subscriptions/listen` stream that replaced it in the 2026-07-28 revision.
The newer one is answered by this server rather than by the SDK's built-in
handling, because that grants the subscription without telling the application
— which would leave a client subscribed to a watcher nobody started, waiting
for events that never come.

Long calls report progress while they run, so a wait shows as running rather
than hung. It costs nothing when the client asks for none.

Four prompts package workflows that are easy to get wrong:
`tmux_run_and_report`, `tmux_diagnose_pane`, `tmux_build_workspace`,
`tmux_interrupt_pane`.

## Which pane am I in?

When the client that launched this server was itself inside tmux, the server
knows its own pane from `TMUX_PANE` and says so in its instructions. Every
pane listing marks it `isCaller`, and `tmux_whoami` answers it directly — so an
assistant can avoid typing into the terminal it is talking through.

## Confirm a build without a client

```console
$ { printf '%s\n' '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"probe","version":"1"}}}'; sleep 1; } | libtmux-mcp
```

The pause matters. Closing standard input immediately is a different test: the
session is torn down while the reply is still being written and you get no
bytes back. A real client holds the stream open for the session, which is what
the pause imitates.

## Standard output belongs to the protocol

Every log line goes to standard error, and the default level is `Warning` so a
working server is quiet. A message written to the wrong stream does not produce
a stray log line — it corrupts the protocol and the client disconnects. That is
worth knowing if you wrap this in something of your own.

## Which tmux it drives

Whatever `tmux` resolves to on the path, or the binary `LIBTMUX_TMUX` names.
The supported range is 3.2a to 3.7b, proven from source on every commit.

If you install the SDK through a version manager rather than system-wide, an
agent that spawns this server will not inherit your shell and the launcher will
not find the runtime. Set `DOTNET_ROOT` in the client's config for that server;
`eng/mcp/mcp_swap.py` does it for you.

## Related packages

| Package | Adds |
|---|---|
| [LibTmux](https://www.nuget.org/packages/LibTmux) | The client this is built on |
| [LibTmux.Query.Json](https://www.nuget.org/packages/LibTmux.Query.Json) | JSON for query documents |
| [LibTmux.Workspace](https://www.nuget.org/packages/LibTmux.Workspace) | Sessions from tmuxp YAML |

Source, docs and issues: <https://github.com/libtmux/libtmux-dotnet>

## License

[MIT](https://github.com/libtmux/libtmux-dotnet/blob/master/LICENSE)
