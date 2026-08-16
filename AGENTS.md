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
