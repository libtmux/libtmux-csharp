# LibTmux

A .NET class library for tmux. Every call reaches a real tmux server, and
which of the three execution modes you are in is visible where the call
starts.

## Start here

[Choosing a mode](modes/matrix.md) shows the same task three ways, with
measured cost for one command and for fifty.

- [One-shot](modes/one-shot.md) — one command, one materialized object
- [Control mode](modes/control-mode.md) — one client, streamed events
- [Chaining](modes/chaining.md) — many commands, one invocation

## Reference

[API reference](api/README.md) is rendered from the doc comments the
compiler emits, so nothing can be documented there and absent from the
library.

## Contracts

The library's surface and behavior are recorded rather than described,
and each record has a validator that fails when the code disagrees.

- [Public API](public-api.md) — the approved surface, rendered from
  `public-api.json`
- [Version deltas](parity/version-deltas.json) — every tmux behavior
  difference the library gates on, each naming the test that proves it
- [Parity ledger](parity/parity-ledger.json) — where each Python libtmux
  symbol went
- [Decisions](decisions/) — why the transport, object model, query
  catalog, and public API are shaped the way they are
