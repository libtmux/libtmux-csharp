"""Render the API reference from the compiler's own XML documentation.

The reference is generated from ``LibTmux.xml`` rather than from the approved
contract, because that file is what the compiler wrote down from the doc
comments on the members themselves. A member whose comment is missing is
missing here too, which is the point: the page is evidence about the comments
rather than a second place to write them.
"""

from __future__ import annotations

import argparse
import pathlib
import sys
import typing as t
from xml.etree import ElementTree

CSHARP_ROOT = pathlib.Path(__file__).parents[2]
OUTPUT_PATH = CSHARP_ROOT / "docs" / "api" / "README.md"
KIND_TITLES = {
    "T": "Types",
    "M": "Methods",
    "P": "Properties",
    "F": "Fields",
    "E": "Events",
}


def documentation_paths() -> list[pathlib.Path]:
    """Return every built XML documentation file."""
    return sorted((CSHARP_ROOT / "src").glob("*/bin/*/net*/LibTmux*.xml"))


def flatten(node: ElementTree.Element | None) -> str:
    """Return one documentation node as a single line of text."""
    if node is None:
        return ""

    return " ".join("".join(node.itertext()).split())


def read_members(path: pathlib.Path) -> dict[str, str]:
    """Return each documented member identifier and its summary."""
    root = ElementTree.parse(path).getroot()
    members: dict[str, str] = {}
    for member in root.findall("./members/member"):
        name = member.get("name")
        if name is None or ".Internal." in name:
            continue

        summary = flatten(member.find("summary"))
        if summary:
            members[name] = summary

    return members


def render(members: dict[str, str]) -> str:
    """Return the reference page for every documented member."""
    grouped: dict[str, list[tuple[str, str]]] = {}
    for name, summary in sorted(members.items()):
        grouped.setdefault(name[:1], []).append((name, summary))

    lines = [
        "# API reference",
        "",
        "Generated from the XML documentation the compiler emits, so every entry",
        "here is the doc comment on the member itself. Regenerate with",
        "`uv run python csharp/eng/docs/render_api_reference.py`.",
        "",
        "See [choosing a mode](../modes/matrix.md) for how the three execution",
        "modes differ.",
    ]
    for kind, title in KIND_TITLES.items():
        entries = grouped.get(kind)
        if not entries:
            continue

        lines.extend(["", f"## {title}", "", "| Member | Summary |", "|---|---|"])
        for name, summary in entries:
            escaped = summary.replace("|", "\\|")
            lines.append(f"| `{name[2:]}` | {escaped} |")

    return "\n".join(lines) + "\n"


def main(arguments: t.Sequence[str] | None = None) -> int:
    """Write the reference, or check the written one is current.

    Examples
    --------
    >>> callable(main)
    True
    """
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    parsed = parser.parse_args(arguments)

    paths = documentation_paths()
    if not paths:
        print("no built XML documentation found; build first", file=sys.stderr)
        return 1

    rendered = render(read_members(paths[0]))
    if parsed.check:
        current = (
            OUTPUT_PATH.read_text(encoding="utf-8") if OUTPUT_PATH.exists() else ""
        )
        if current != rendered:
            print(
                "api reference differs from the built XML documentation",
                file=sys.stderr,
            )
            return 1

        return 0

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(rendered, encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
