# AGENTS.md

Guidance for AI agents working on LibTmux, a .NET client for tmux.

## Own your tmux sockets

Several libtmux ports live on this machine and run real tmux at the same time.
A socket in the default root is reachable by all of them, so one port's cleanup
sweep kills another port's servers mid-run — and the failure surfaces in
whichever suite noticed first, which is rarely the one that caused it. That
misattribution is what turns socket sharing into a debugging loop.

Give this repository a socket root of its own, named for the port and what it
is for:

- Tests: `TMUX_TMPDIR=/tmp/libtmux-dotnet-test`
- Servers you start by hand: `TMUX_TMPDIR=/tmp/libtmux-dotnet-dev`

tmux reads `TMUX_TMPDIR` when it execs and puts a `-L name` socket in
`$TMUX_TMPDIR/tmux-$UID/name`, so exporting it before the run is enough. A
`-S path` socket ignores it, and needs a path under the root instead.

Two things are never safe here, because the processes and directories belong to
other workspaces:

- `pkill tmux`, or any kill by a pattern matching more than your own root
- deleting `/tmp/tmux-$UID/` or another port's root

To find what this repository left behind, list its root rather than matching
process names. A socket file outlives the server that made it, so read the
listing as candidates and confirm each with `has-session`:

```console
$ ls /tmp/libtmux-dotnet-test/tmux-$(id -u)
```

## The toolchain is not on `PATH`

`dotnet` is pinned by `global.json` and resolves through mise:

```console
$ mise exec -- dotnet build LibTmux.slnx --configuration Release --warnaserror
```

## What gates this repository

`.github/workflows/dotnet.yml` is the source of truth, and its `gate` job is
the single name branch protection requires — adding a job means adding it to
`gate`'s `needs`, not to a protection rule. Beyond building and `dotnet test`,
two validators run on documents rather than the build, and are easy to forget
locally:

```console
$ uv run python eng/parity/verify_public_api.py
```

```console
$ uv run python eng/parity/verify_capabilities.py
```

The packaging tests inside the integration suite read what a pack produced, so
they fail on a tree nobody packed. Run the build workflow's order — pack, then
the package consumer, then the ahead-of-time publish — before the integration
suite, or expect `PackageClosureTests` to fail for a reason that is not a bug.

Publishing ahead of time names a runtime identifier, and restore then writes
one into the lock file of every project in that graph — including the library's,
where the section is empty because no package resolves differently. That is why
`src/LibTmux` and `src/LibTmux.Generators` declare `RuntimeIdentifiers`: without
it, the lock files disagree with the projects and the *next*
`restore --locked-mode` fails with NU1004, which reads like a dependency problem
and is not one. Adding a platform to the matrix means adding its identifier
there and regenerating:

```console
$ mise exec -- dotnet restore LibTmux.slnx --force-evaluate
```

`.github/workflows/dotnet-tmux.yml` builds each supported tmux from source and
runs the integration suite against it, behind a `compatibility` job that plays
the same role as `gate`. That is what proves the compatibility range; the build
workflow only ever sees whatever tmux Ubuntu ships.

`dotnet.yml` also carries an advisory `macos arm64` lane, because the
compatibility claim names macOS and a claim nobody runs is a claim. Its first
run failed 15 of 854 integration tests, so it stays outside `gate` until those
are diagnosed: requiring it would block every commit on an undiagnosed platform
difference, and deleting it would go back to not knowing. It restores
without `--locked-mode`: the lock files are generated for the Linux runtime
identifiers this repository publishes, so locking a macOS restore would fail for
a reason that is not a dependency problem.

Two more workflows run on a schedule rather than on the gate, because what they
check can change without a commit: `codeql.yml` analyses the build, and
`scorecard.yml` scores the repository's supply chain. Every action reference in
this repository is pinned to a commit SHA with the version in a trailing
comment, which is what stops a moved tag from changing what CI runs. Dependabot
maintains those pins; a pin nobody updates is just a stale action.

## The Python original is a separate checkout

This repository was imported out of a monorepo that also held Python libtmux,
so anything grounded in that source needs to be told where it went now:

```console
$ LIBTMUX_PYTHON_REPOSITORY=~/work/python/libtmux uv run python eng/parity/verify_ledger.py
```

## Recorded evidence is a release artifact

A capability row is `pending` until a matrix run records evidence for it, and
`verified` after. What `verified` claims is exact: these tmux versions, on these
frameworks, at *this tree* — the fingerprint covers every tracked file outside
the evidence directory. Any commit changes it, so a verified row is true at one
commit and stale at the next.

That is why recording belongs at a release boundary rather than in the gate,
and why `reconcile_versions.py` and `verify_ledger.py` are not in
`.github/workflows/dotnet.yml`. Between releases every row is `pending`, which
is the honest state: nobody has run the matrix against this tree.

To record, on the commit being released:

```console
$ eng/tmux/run-matrix.sh --evidence-dir docs/parity/evidence/0001 --capability-cohort 0001 tests/LibTmux.IntegrationTests/LibTmux.IntegrationTests.csproj
```

```console
$ uv run python eng/parity/reconcile_versions.py --evidence docs/parity/evidence/0001/results.ndjson --write
```

Then commit the bundle and the rewritten `version-deltas.json` together, because
the fingerprint is of the tree that commit produces. A tmux build takes about
forty seconds here and the matrix runs the suite fourteen times, so budget half
an hour.

## Comments earn their maintenance cost

A comment ships only if it passes all three gates. Fail any: delete or rewrite.
Borderline: delete — borderline means the information is reconstructible, which
is what makes deletion cheap.

**Loss.** Three years from now, would losing this cost a maintainer real time
rediscovering intent, an invariant, a constraint, or a failure mode the code and
tests do not already make obvious?

**Elite.** Would SQLite, Redis, the Go standard library, or CPython write this
comment, at this length? Those projects state the constraint and stop. They do
not argue with an imagined objector.

**Upkeep.** Will it stay true without maintenance? A comment that hand-syncs a
value the code owns — a count, an offset, a line reference, a duplicated
constant — is false the first time that value moves.

### Ceiling

One or two lines. A comment reaching four is either carrying several facts, in
which case split it, or arguing, in which case cut it to the fact.

Rationale, alternatives weighed, and the story of how the code got here belong
in the commit message: timestamped, attached to the exact diff, and free to
maintain.

A comment often holds both a constraint and the deliberation that found it. Keep
the constraint, cut the deliberation. "Runs at most once per second" survives;
"this is the right trade for now" does not.

### Keep

- Why over how: upstream quirks, protocol and compatibility constraints,
  performance tradeoffs still part of the contract.
- Invariants, preconditions, ordering, lifetime, and concurrency requirements
  that types and tests cannot express.
- Code that looks wrong but is not, so a later cleanup does not reintroduce the
  bug.
- A high-level sketch of an algorithm whose local operations do not reveal the
  whole.

### Delete

- Narration of the next lines; code translated into English.
- Restated names, types, defaults, or control flow.
- Values duplicated from the code and hand-synced.
- Justification, hedging, or apology for a choice.
- Speculation about future requirements.
- History version control already holds, including commented-out code.
- Ticket and issue numbers. They say nothing to a reader without tracker access,
  and they rot when the tracker moves. Unfinished work goes in the tracker, not
  the source.
- Transient observations — "currently", "for now", "the latest release" —
  that go stale with no nearby edit.

### The upkeep gate in practice

It reaches values that track our own code. It does not reach frozen external
facts.

Bad (Delete):

```csharp
// There are 321 tests to complete for servers.
```

Good (Keep):

```csharp
// tmux < 3.2 reports the pane ID only after the command completes,
// so this query must stay separate.
```

### Documentation exception

Doctests, minimal usage examples, and param, return, and raises lines on public
API are exempt from the loss gate — they serve the caller, not the maintainer.
They are exempt from nothing else. Ceiling: a good man page entry.

XML documentation — `<summary>`, `<param>`, `<returns>` — falls under this
exception; `CS1591` is unsuppressed in the published projects.

## Git Commit Standards

Format commit messages as:
```
Scope(type[detail]): concise description

why: Explanation of necessity or impact.

what:
- Specific technical changes made
- Focused on a single topic
```

Keep the subject ≤50 chars (excluding any trailing `(#NN)` PR ref); wrap
body lines at ≤72 chars. Separate the `why:` and `what:` blocks with a
blank line.

Common commit types:
- **feat**: New features or enhancements
- **fix**: Bug fixes
- **refactor**: Code restructuring without functional change
- **docs**: Documentation updates
- **chore**: Maintenance (dependencies, tooling, config)
- **test**: Test-related updates
- **style**: Code style and formatting
- **dotnet(deps)**: Dependencies
- **dotnet(deps[dev])**: Dev Dependencies
- **ai(rules[AGENTS])**: AI rule updates

Example:
```
Pane(feat[SendKeys]): Add support for a literal flag

why: Send characters without tmux interpreting them.

what:
- Add a Literal property to SendKeysOptions
- Pass -l when it is set
```

### Release commits

Never create tags. Never push tags. The user handles tagging and tag
pushes (tags trigger the CI publish workflow).

Release commit subjects are plain and short: `Tag v<version>`. Put
the detailed why/what in the commit body. Don't use the
`Scope(type[detail]):` format for releases — don't bury the lede.

For multi-line commits, use heredoc to preserve formatting:
```bash
git commit -m "$(cat <<'EOF'
Scope(feat[detail]): Concise description

why: Explanation of the change.

what:
- First change
- Second change
EOF
)"
```

## Code Blocks

Code blocks are paste-and-run units: pasting one block runs exactly one
intended action. Doctests and other executed examples are exempt — the test
suite runs them, nobody pastes them.

- **One command per block.** Multiple steps may share a block only when
  explicitly chained with `&&`, `;`, or `\` continuations — the chain is
  then one logical command.
- **Explanations go in prose above the block**, never as `#` comments inside it.
- **Command menus are per-command blocks with prose lead-ins**, not tables.
- **Shell commands use the `console` tag with a `$ ` prefix.** This separates
  interactive commands from scripts and enables prompt-aware copy.
- **Split long commands with `\`** — one flag or flag+value pair per indented
  continuation line, positional arguments last.

Good:

Show the last ten commits as a graph:

```console
$ git log \
    --max-count=10 \
    --graph \
    --oneline
```

Bad:

```console
# Show the last ten commits as a graph
$ git log --max-count=10 --graph --oneline
```
