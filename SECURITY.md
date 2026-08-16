# Security policy

## Reporting a vulnerability

Report privately through GitHub's
[security advisories](https://github.com/libtmux/libtmux-dotnet/security/advisories/new)
rather than as a public issue, so a fix can exist before the details do.

Expect an acknowledgement within a week. If a report is a real vulnerability,
the advisory will name the affected versions and the release that fixes it.

## What is in scope

This library builds command lines for tmux and parses what tmux prints, which
is where its interesting failure modes live:

- **Argument injection.** A session, window, or pane name that escapes its
  argument and becomes a separate tmux command.
- **Parser failures on hostile input.** Output from a tmux — or from something
  pretending to be one — that crashes, hangs, or corrupts state rather than
  being rejected. The [fuzz
  tests](tests/LibTmux.UnitTests/Fuzzing/ParserFuzzTests.cs) cover the known
  shapes; anything they miss is worth reporting.
- **Leaking a socket to the wrong process**, or reaching a tmux server the
  caller did not name.

## What is not

- **tmux's own vulnerabilities.** Report those to
  [tmux](https://github.com/tmux/tmux). This library is a client.
- **What a caller does with a pane.** Sending arbitrary text to a shell is the
  purpose of `SendTextAsync`, not a flaw in it. A caller passing untrusted
  input to it has an untrusted-input problem.
- **The MCP server's permissiveness.** `LibTmux.Mcp` gives an assistant the
  ability to run commands in a terminal. That is what it is for; run it where
  that is acceptable.

## Supported versions

While this is alpha, only the latest prerelease is supported. Fixes go into the
next version rather than into patches of earlier ones. A published version is
never deleted, so an old alpha stays installable and stays unfixed — pin
deliberately.
