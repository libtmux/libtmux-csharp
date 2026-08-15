# ADR 0005: Reconciling the contract with the built assembly

## Status

Accepted. Amends the member surface approved in
[decision 0004](0004-public-api-approval.md); the packages, namespaces,
ownership model, and query boundary it settled are unchanged.

## Context

Decision 0004 approved a member surface before the code existed, which is what
made it a contract rather than a description. Until something read both, the
two could disagree without saying so: the type-level check held names to the
assembly, and members were compared only by the RS0016 analyzer, which reads
the assembly against its own baseline and never against the approved surface.

Reading them found twenty-four members the contract names and the assembly
spells otherwise. They are not one problem. Some are places where building the
thing taught us something the approval could not know; the rest are approved
surface that was reserved and then not built, where the contract is right and
the absence is the defect.

Each is settled below, because a divergence nobody has ruled on reads as
approval either way.

## Decision

### The build's shape wins where it learned something

**`SnapshotDepth` names the hierarchy, not an abstraction of it.** Approved as
`Scalar`, `Children`, and `Hierarchy`; built as `Server`, `Sessions`,
`Windows`, and `Panes`. tmux has four levels a capture can stop at, and
stopping after sessions is a different answer than stopping after windows —
one costs `list-sessions`, the other adds `list-windows -a`. Three abstract
degrees cannot express the level a caller is choosing, and naming the level
makes the cost of the choice legible. The contract takes the built spelling.

**`CapturedRelation<T>` is the list rather than a way to get one.** Approved
with `GetItems()` and `TryGetItems(out …)`; built as an `IReadOnlyList<T>` that
answers `IsCaptured` and `OrEmpty()`. The approved pair made every read of a
captured relation a two-step, and a caller who skipped the check got an
exception from a type that already knew the answer. Implementing the list
interface directly keeps "nobody looked" and "there are none" distinguishable
through `IsCaptured` while letting the common path be a `foreach`. The
contract takes the built shape.

**`IncompleteSnapshotException` carries the depth that made it incomplete.**
Approved as `(message, relationName, inner)` with a `RelationName` property;
built as `(relation, capturedDepth)` with `Relation` and `CapturedDepth`. The
question a caller has on catching it is "how deep did the capture go, and was
that enough" — which the approved shape cannot answer and the built one does,
composing its own message from both. The contract takes the built shape.

**`SessionWindowEdge` is a record with an optional ordinal.** Approved as a
`readonly record struct` positional in `(SessionId, WindowId, int index,
int edgeOrdinal)`; built as a `sealed record` with required `SessionId`,
`WindowId`, and `WindowIndex`, an `int? Ordinal`, and a `Key`. Two things
forced it: an edge exists before a snapshot has ordered a session's edges, so
the ordinal is genuinely absent rather than zero, and `index` was ambiguous
between the tmux window index and the position in the session's order — the
built names say which is which. The contract takes the built shape.

**`WindowEntityKey` joins a session to a window.** Approved as
`(ServerGeneration, WindowId)`; built as `(SessionId, WindowId)`. tmux links
one window into several sessions at different indexes, so a window identifier
plus a generation names a window but not a position in the hierarchy, which is
the thing the key exists to identify. The contract takes the built shape.

### The contract's shape wins where the surface was simply not built

The rest are members decision 0004 approved and the build has not reached.
They are built to the approved signatures rather than removed, because the
snapshot they belong to is half-finished, not superseded: the capture already
records sessions and session-to-window edges, and its own remarks point at the
per-entity relations as where that linkage is read.

- `Session.Windows`, `Session.Panes`, `Window.Panes`, and
  `Window.LinkedSessions` read what a capture found, without reaching tmux.
- `Window.Edge` and `Window.EntityKey` name where a window sits, which a
  window identifier alone cannot say.
- `Session.RawFormatFields`, `Window.RawFormatFields`, and
  `Pane.RawFormatFields` expose the fields a handle materialized from, as
  `Client.RawFormatFields` already does.
- `Session.Attached` reports whether the session had a client when it was read.
- `Session.GetWindowAsync(string, CancellationToken)` resolves one window by
  target within the session that owns it.
- `UnsupportedQueryExpressionException` gains the expression-carrying
  constructor and `Expression` property: a translation failure that does not
  name the expression it refused makes the caller find it by bisection. Its
  message-only constructor is recorded rather than removed, because a refusal
  that names a field or a constant type has no single expression to carry.

## Consequences

`docs/public-api.json` is amended for the five decisions above, and
`docs/public-api.md` is regenerated from it. `verify_public_api.py`
pins the `SnapshotDepth` sentinel values, so it is amended with the contract.

Members are now compared in one direction on every build: every member the
contract names must exist in the assembly, matched by name and argument count.
The reverse stays with the analyzer, which fails the build on a public member
missing from the shipped baseline.

The approved-but-unbuilt members are built rather than tracked, so the list of
tolerated member divergences is empty and the mechanism that tolerated them is
gone. A future divergence is a failing test on the commit that introduces it,
not a line in a list.
