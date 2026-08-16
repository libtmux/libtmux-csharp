# One-shot: the default

A one-shot call starts a tmux client, runs one command, waits for it, and lets
the client exit. It is what every typed method on `Server`, `Session`,
`Window`, and `Pane` does unless you asked for something else.

<!-- snippet: CreateWindow -->
```csharp
Window window = await session.CreateWindowAsync(new NewWindowRequest(name: "build"), ct);
Console.WriteLine($"{window.Id} {window.Index}:{window.Name}");
```
<!-- endsnippet -->

Example output:

```
@1 1:build
```

## When this is the right mode

Almost always. One command, one materialized object, and the object is a
reading rather than a live view: it keeps saying what tmux reported when it was
made. Refresh is explicit, so nothing changes under you mid-function.

## When it is not

Starting a process costs more than running a command does. For a handful of
commands that is invisible; for fifty in a row it dominates, and
[chaining](chaining.md) pays that cost once instead of fifty times.

It also only ever sees what it asked for. To notice a window appearing, or read
what a program writes into a pane, you need a client that stays —
[control mode](control-mode.md).

The transport this uses, and the two shapes it beat, are recorded in
[ADR 0001](../decisions/0001-transport-framing-bakeoff.md).

What each mode costs, measured for one command and for fifty, is in
[choosing a mode](matrix.md).
