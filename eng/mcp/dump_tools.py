#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.10"
# dependencies = []
# ///
"""Write the tool reference from what the server actually advertises.

A hand-written tool table is wrong the first time somebody adds a tool and
forgets the table. This asks the server, so the document cannot describe a
surface that is not there.

Run it after changing the tool surface:

```console
$ uv run eng/mcp/dump_tools.py
```

``--check`` writes nothing and fails when the committed document no longer
matches the server, which is what makes the document a record rather than a
description:

```console
$ uv run eng/mcp/dump_tools.py --check
```
"""

from __future__ import annotations

import difflib
import json
import os
import pathlib
import subprocess
import sys
import time

REPO = pathlib.Path(__file__).resolve().parents[2]
OUTPUT = REPO / "docs" / "mcp" / "tools.md"

#: Every tier, so the reference covers tools the default tier hides.
TIERS = ("readonly", "mutating", "destructive")

FRAMES = (
    {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "initialize",
        "params": {
            "protocolVersion": "2025-06-18",
            "capabilities": {},
            "clientInfo": {"name": "dump_tools", "version": "1"},
        },
    },
    {"jsonrpc": "2.0", "method": "notifications/initialized"},
    {"jsonrpc": "2.0", "id": 2, "method": "tools/list"},
    {"jsonrpc": "2.0", "id": 3, "method": "resources/list"},
    {"jsonrpc": "2.0", "id": 4, "method": "resources/templates/list"},
    {"jsonrpc": "2.0", "id": 5, "method": "prompts/list"},
)


def _binary() -> pathlib.Path:
    for framework in ("net10.0", "net8.0"):
        candidate = (
            REPO / "src" / "LibTmux.Mcp" / "bin" / "Release" / framework / "LibTmux.Mcp"
        )
        if candidate.is_file():
            return candidate
    msg = "build LibTmux.Mcp in Release first"
    raise SystemExit(msg)


def _ask(tier: str) -> dict[int, dict]:
    """Run one server at ``tier`` and collect its answers by request id."""
    proc = subprocess.Popen(
        [str(_binary())],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        text=True,
        bufsize=1,
        # The ambient environment, so the apphost finds the runtime the same
        # way the developer's shell does. Only the tier is overridden.
        env={**os.environ, "LIBTMUX_SAFETY": tier},
    )
    assert proc.stdin is not None
    assert proc.stdout is not None

    answers: dict[int, dict] = {}
    wanted = {frame["id"] for frame in FRAMES if "id" in frame}
    for frame in FRAMES:
        proc.stdin.write(json.dumps(frame) + "\n")
        proc.stdin.flush()

    deadline = time.monotonic() + 30
    while answers.keys() != wanted and time.monotonic() < deadline:
        line = proc.stdout.readline()
        if not line:
            break
        try:
            message = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(message, dict) and "id" in message and "result" in message:
            answers[message["id"]] = message["result"]

    proc.stdin.close()
    try:
        proc.wait(timeout=10)
    except subprocess.TimeoutExpired:
        proc.kill()
    return answers


def _tier_of(name: str, by_tier: dict[str, set[str]]) -> str:
    for tier in TIERS:
        if name in by_tier[tier]:
            return tier
    return "unknown"


def _one_line(text: str) -> str:
    """Collapse a description to its first sentence, for a table cell."""
    flat = " ".join(text.split())
    stop = flat.find(". ")
    return flat if stop < 0 else flat[: stop + 1]


def main() -> int:
    answers = {tier: _ask(tier) for tier in TIERS}
    if not answers["destructive"].get(2):
        print("the server did not answer tools/list", file=sys.stderr)
        return 1

    by_tier = {
        tier: {tool["name"] for tool in answers[tier][2]["tools"]} for tier in TIERS
    }
    tools = sorted(answers["destructive"][2]["tools"], key=lambda tool: tool["name"])

    lines = [
        "# tmux MCP tools",
        "",
        "Generated from the server itself — a table nobody generates is wrong the",
        "first time somebody adds a tool. Regenerate after changing the surface:",
        "",
        "```console",
        "$ uv run eng/mcp/dump_tools.py",
        "```",
        "",
        f"{len(tools)} tools, {len(answers['destructive'].get(3, {}).get('resources', []))}"
        f" resources and"
        f" {len(answers['destructive'].get(4, {}).get('resourceTemplates', []))} resource"
        " templates, and"
        f" {len(answers['destructive'].get(5, {}).get('prompts', []))} prompts.",
        "",
        "`tier` is the lowest `LIBTMUX_SAFETY` that registers the tool. `read` marks",
        "a tool annotated read-only, which a client may use to skip a confirmation.",
        "",
        "| Tool | Tier | Read | Does |",
        "|---|---|---|---|",
    ]
    for tool in tools:
        annotations = tool.get("annotations") or {}
        read = "yes" if annotations.get("readOnlyHint") else ""
        lines.append(
            f"| `{tool['name']}` | {_tier_of(tool['name'], by_tier)} | {read} "
            f"| {_one_line(tool.get('description', ''))} |"
        )

    for label, key, field, uri in (
        ("Resources", 3, "resources", "uri"),
        ("Resource templates", 4, "resourceTemplates", "uriTemplate"),
    ):
        entries = answers["destructive"].get(key, {}).get(field, [])
        if not entries:
            continue
        lines += ["", f"## {label}", "", "| URI | Does |", "|---|---|"]
        lines += [
            f"| `{entry[uri]}` | {_one_line(entry.get('description', ''))} |"
            for entry in sorted(entries, key=lambda entry: entry[uri])
        ]

    prompts = answers["destructive"].get(5, {}).get("prompts", [])
    if prompts:
        lines += ["", "## Prompts", "", "| Prompt | Does |", "|---|---|"]
        lines += [
            f"| `{prompt['name']}` | {_one_line(prompt.get('description', ''))} |"
            for prompt in sorted(prompts, key=lambda prompt: prompt["name"])
        ]

    rendered = "\n".join(lines) + "\n"
    if "--check" in sys.argv[1:]:
        current = OUTPUT.read_text() if OUTPUT.is_file() else ""
        if current == rendered:
            print(f"{OUTPUT.relative_to(REPO)} is current ({len(tools)} tools)")
            return 0
        print(
            f"{OUTPUT.relative_to(REPO)} is stale; run: uv run eng/mcp/dump_tools.py",
            file=sys.stderr,
        )
        for line in difflib.unified_diff(
            current.splitlines(),
            rendered.splitlines(),
            fromfile="committed",
            tofile="server",
            lineterm="",
        ):
            print(line, file=sys.stderr)
        return 1

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(rendered)
    print(f"wrote {OUTPUT.relative_to(REPO)} ({len(tools)} tools)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
