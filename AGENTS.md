# AGENTS.md

Guidance for AI agents working on LibTmux, a .NET client for tmux.

## Own your tmux sockets

Several libtmux ports live on this machine and run real tmux at the same time.
A socket in the default root is reachable by all of them, so one port's cleanup
sweep kills another port's servers mid-run — and the failure surfaces in
whichever suite noticed first, which is rarely the one that caused it. That
misattribution is what turns socket sharing into a debugging loop.

Give this repository a socket root of its own, named for the language and what
it is for:

- Tests: `TMUX_TMPDIR=/tmp/libtmux-csharp-test`
- Servers you start by hand: `TMUX_TMPDIR=/tmp/libtmux-csharp-dev`

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
$ ls /tmp/libtmux-csharp-test/tmux-$(id -u)
```

## The toolchain is not on `PATH`

`dotnet` is pinned by `global.json` and resolves through mise:

```console
$ mise exec -- dotnet build LibTmux.slnx --configuration Release --warnaserror
```

## What gates this repository

`.github/workflows/csharp.yml` is the source of truth. Beyond building and
`dotnet test`, two validators run on documents rather than the build, and are
easy to forget locally:

```console
$ uv run python eng/parity/verify_public_api.py
```

```console
$ uv run python eng/parity/verify_capabilities.py
```
