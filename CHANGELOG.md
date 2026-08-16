# Changelog

nuget.org shows release notes per version, so somebody landing on the newest
package never learns what an older one fixed. This is the whole history in one
place.

Versions follow [Semantic Versioning](https://semver.org). During alpha the
public API can change in any release with no deprecation period — pin an exact
version.

## Unreleased

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

[0.0.0-alpha.3]: https://github.com/libtmux/libtmux-dotnet/releases/tag/v0.0.0-alpha.3
[0.0.0-alpha.2]: https://github.com/libtmux/libtmux-dotnet/releases/tag/v0.0.0-alpha.2
[0.0.0-alpha.1]: https://github.com/libtmux/libtmux-dotnet/releases/tag/v0.0.0-alpha.1
