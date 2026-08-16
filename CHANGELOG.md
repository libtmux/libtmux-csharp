# Changelog

nuget.org shows release notes per version, so somebody landing on the newest
package never learns what an older one fixed. This is the whole history in one
place.

Versions follow [Semantic Versioning](https://semver.org). During alpha the
public API can change in any release with no deprecation period — pin an exact
version.

## [0.0.0-alpha.6] — 2026-08-16

No change to the library. `git diff v0.0.0-alpha.5..v0.0.0-alpha.6 -- src/`
is empty, so this package is the alpha.5 package.

### Changed

- **macOS arm64 is proven rather than advisory-and-failing.** The lane had
  failed two integration tests since it was added, recorded as timing. It was
  pane width: a GitHub macOS runner's hostname is 61 characters, so bash's
  prompt fills 78 of the pane's 80 columns, and tmux stores the resulting wrap
  as a real line break. Text typed there arrives split, so a capture joined
  with newlines cannot contain it. Those assertions now capture with
  `joinWrappedLines`, which is what `capture-pane -J` is for. A caller
  asserting on pane text hits the same thing on any machine with a long
  prompt.

## [0.0.0-alpha.5] — 2026-08-16

### Added

- **`LIBTMUX_SOCKET_NAME` and `LIBTMUX_SOCKET_PATH` point a connection that
  named no socket.** A harness, sandbox or container can move every
  unqualified connection at once without the call sites in between naming a
  socket they should not have to know about. A caller who named one — by path,
  by name, or by factory — is never redirected, and between the two variables
  the path wins. Adds no public member: the existing resolution path reads them
  where it already reads `TMUX_TMPDIR`.
- **Every documented example is executed against live tmux in CI**, not only
  compiled. The examples are real methods that run as tests, and the blocks the
  documents publish are copied from that code by
  `eng/docs/sync_snippets.py`, which fails the build when the two drift. The
  first example in the README — a bare `Server.ConnectAsync()` — could not run
  under the old harness and now does.

### Fixed

- **A control-mode command that ends the client no longer shifts every later
  reply by one.** The waiter is queued before the write, so the exit sweep
  finds it; a write that then fails marks the slot abandoned rather than
  leaving one for a command tmux never saw.
- **Disposing a control session no longer kills the tmux server.** Only the
  client is killed, so other clients — and other sessions on the same socket —
  survive.
- Control mode survives a consumer that stops reading its event stream.
- A chain refuses a target read from a server that has since restarted, rather
  than aiming a reused id at a different object.
- A query document is treated as input rather than as instructions: an unknown
  field name is refused against the closed catalog instead of resolving to any
  public property.
- macOS: a pane's path is compared resolved, since `/tmp` is a symlink there,
  and the suite no longer assumes `/bin/sh` reports as `sh`.

### Changed

- The macOS lane runs as an advisory job and reports what it finds, rather than
  the compatibility claim naming a platform nobody ran.

## [0.0.0-alpha.4] — 2026-08-16

### Added

- **`TmuxDispatchState` on every failure**, so a caller can tell a safe retry
  from one that repeats a side effect. `NotDispatched` is claimed only where the
  library can see that no tmux process ran; `TmuxCommandException` is always
  `Dispatched` because it exists because tmux answered; the default is `Unknown`,
  since a client that died mid-command may have been obeyed first. Reads as an
  exception filter:
  `catch (LibTmuxException e) when (e.Dispatch == TmuxDispatchState.NotDispatched)`.
- **Fuzz tests over the parsers** — 8,000 generated cases and a corpus of the
  shapes that break parsers, asserting a refusal rather than a crash or a hang.
- **Recorded benchmarks** under [`docs/benchmarks`](docs/benchmarks/README.md),
  each naming its tmux, host, runtime, commit and date, with the full
  distribution over 100 samples.
- **`SECURITY.md`**, and CodeQL and OpenSSF Scorecard workflows.
- `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, and this file.

### Changed

- **The benchmark numbers in the README were wrong and are now marginal costs.**
  The table claimed chaining fifty commands cost less than chaining one, which
  cannot be true. It was not noise: it reproduced across two independent
  100-sample runs because five warmup samples were not enough to outlast the
  runtime tiering up, and the penalty always landed on whichever case ran first.
  At forty warmup samples the order corrects. The front page now quotes what one
  more command costs — the part that belongs to the library rather than the
  machine — and links to a recorded run for the absolute milliseconds.
- The documented claim that chaining beats control mode for a batch is now
  stated as host-dependent, because it reverses on a loaded machine.
- Every GitHub Actions reference is pinned to a commit SHA, maintained by
  Dependabot.
- Example coverage extended to `docs/modes/`, so the documents that teach the
  API are compiled and run rather than only the ones that ship.
- Dependencies updated across the board, including four major bumps:
  `ModelContextProtocol` 0.4.0-preview.1 to 2.2.0, `YamlDotNet` 16 to 18, and
  both xunit packages to 4.0.0. No consumer-visible behaviour changed; the core
  library's single dependency is unaffected.

## [0.0.0-alpha.3] — 2026-08-16

### Changed

- The project moved to `github.com/libtmux/libtmux-dotnet`. This is the first
  build whose project and repository links are that address rather than a
  redirect to it. No API, behaviour or dependency difference from alpha.2.

## [0.0.0-alpha.2] — 2026-08-15

### Fixed

- **A filter can be written over the objects the library hands back.**
  `sessions.Matching(s => s.Name.StartsWith("build"))` now translates; before,
  only a projection row whose properties were spelled like tmux fields did. The
  field catalog now carries the CLR property for each of the twelve queryable
  fields, in both directions.
- **net8.0 consumers take an 8.0 logging abstraction** rather than a 10.0 one,
  so the package no longer pulls a newer runtime into an 8.0 application.
- `Server.GetSessionsAsync` no longer returns an empty list when the endpoint
  reported no tmux version; that failure now surfaces instead of reading as
  "no sessions".
- The MCP server reports its real version rather than `0.0.0.0`.

## [0.0.0-alpha.1] — 2026-08-15

First public release. `0.0.0-alpha.1` is the lowest version that still says what
it is: a published version can never be deleted from nuget.org, only unlisted.

- `LibTmux` — the client. Servers, sessions, windows, panes, clients, options,
  hooks and buffers, typed and asynchronous, against tmux 3.2a through 3.7b on
  net8.0 and net10.0. One dependency.
- `LibTmux.Query.Json` — `System.Text.Json` for query documents.
- `LibTmux.Workspace` — sessions from tmuxp workspace files.
- `LibTmux.Mcp` — a Model Context Protocol server, installed as a .NET tool.

[0.0.0-alpha.4]: https://github.com/libtmux/libtmux-dotnet/releases/tag/v0.0.0-alpha.4
[0.0.0-alpha.3]: https://github.com/libtmux/libtmux-dotnet/releases/tag/v0.0.0-alpha.3
[0.0.0-alpha.2]: https://github.com/libtmux/libtmux-dotnet/releases/tag/v0.0.0-alpha.2
[0.0.0-alpha.1]: https://github.com/libtmux/libtmux-dotnet/releases/tag/v0.0.0-alpha.1
