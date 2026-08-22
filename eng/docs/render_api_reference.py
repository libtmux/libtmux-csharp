"""Render the API reference from the compiler's own XML documentation.

The reference takes summaries from ``LibTmux.xml`` and visibility from the
approved contract. The compiler XML contains comments for internal helpers too;
only exact public member identifiers approved for the core package may render.
"""

from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import json
import pathlib
import re
import sys
import typing as t
from xml.etree import ElementTree

CSHARP_ROOT = pathlib.Path(__file__).parents[2]
OUTPUT_PATH = CSHARP_ROOT / "docs" / "api" / "README.md"
PUBLIC_API_PATH = CSHARP_ROOT / "docs" / "public-api.json"
KIND_TITLES = {
    "T": "Types",
    "M": "Methods",
    "P": "Properties",
    "F": "Fields",
    "E": "Events",
}
MemberShape = tuple[str, str, str, int, int]


def documentation_paths() -> list[pathlib.Path]:
    """Return built core XML documentation, preferring the CI configuration."""
    paths = (CSHARP_ROOT / "src" / "LibTmux" / "bin").glob(
        "*/net*/LibTmux.xml"
    )
    return sorted(
        paths,
        key=lambda path: (
            path.parent.parent.name != "Release",
            path.parent.name != "net10.0",
            str(path),
        ),
    )


def public_type_names(path: pathlib.Path = PUBLIC_API_PATH) -> frozenset[str]:
    """Return the contract names of public types in the core assembly."""
    contract = json.loads(path.read_text(encoding="utf-8"))
    return frozenset(
        entry["id"][2:]
        for entry in contract["types"]
        if entry["package"] == "LibTmux" and "public" in entry["modifiers"]
    )


def contract_surface(
    path: pathlib.Path = PUBLIC_API_PATH,
) -> tuple[frozenset[str], Counter[MemberShape]]:
    """Return approved public types and member shapes for the core assembly."""
    contract = json.loads(path.read_text(encoding="utf-8"))
    public_types = {
        entry["id"]
        for entry in contract["types"]
        if entry["package"] == "LibTmux" and "public" in entry["modifiers"]
    }
    public_members = [
        entry
        for entry in contract["members"]
        if entry.get("package") == "LibTmux"
        and entry.get("visibility") in {"public", "explicit"}
        and entry.get("declaringType") in public_types
        and entry.get("kind") != "type"
    ]
    shapes: Counter[MemberShape] = Counter(
        (
            entry["id"][0],
            entry["declaringType"][2:],
            entry["name"],
            len(entry.get("genericParameters", [])),
            len(entry.get("parameters", [])),
        )
        for entry in public_members
    )
    return frozenset(public_types), shapes


def public_member_ids(
    documentation_path: pathlib.Path,
    contract_path: pathlib.Path = PUBLIC_API_PATH,
) -> frozenset[str]:
    """Map approved public source IDs to exact compiler XML member IDs."""
    public_types, approved_shapes = contract_surface(contract_path)
    type_names = frozenset(identifier[2:] for identifier in public_types)
    candidates: dict[MemberShape, list[str]] = defaultdict(list)
    selected = set(public_types)
    root = ElementTree.parse(documentation_path).getroot()
    for member in root.findall("./members/member"):
        name = member.get("name")
        if name is None:
            continue
        shape = xml_member_shape(name, type_names)
        if shape is not None and shape in approved_shapes:
            candidates[shape].append(name)

    for shape, identifiers in candidates.items():
        if len(identifiers) > approved_shapes[shape]:
            joined = ", ".join(sorted(identifiers))
            raise ValueError(
                "XML documentation has more members than the approved public shape "
                f"{shape}: {joined}"
            )
        selected.update(identifiers)
    return frozenset(selected)


def xml_member_shape(
    identifier: str,
    public_types: frozenset[str],
) -> MemberShape | None:
    """Return the contract-comparable shape of one compiler XML identifier."""
    if len(identifier) < 3 or identifier[1] != ":" or identifier[0] == "T":
        return None

    body = identifier[2:]
    declaring = next(
        (
            type_name
            for type_name in sorted(public_types, key=len, reverse=True)
            if body.startswith(f"{type_name}.")
        ),
        None,
    )
    if declaring is None:
        return None

    member = body[len(declaring) + 1 :]
    head, separator, parameters = member.partition("(")
    arity_match = re.search(r"``(?P<arity>[1-9][0-9]*)$", head)
    generic_arity = int(arity_match.group("arity")) if arity_match else 0
    if arity_match:
        head = head[: arity_match.start()]
    parameter_count = count_xml_parameters(parameters) if separator else 0
    return (
        identifier[0],
        declaring,
        head.replace("#", "."),
        generic_arity,
        parameter_count,
    )


def count_xml_parameters(parameters: str) -> int:
    """Count top-level parameters in the tail of a compiler XML identifier."""
    closing = parameters.rfind(")")
    if closing < 0:
        raise ValueError("Malformed XML documentation member identifier.")
    body = parameters[:closing]
    if not body:
        return 0

    depth = 0
    count = 1
    for character in body:
        if character in "{[":
            depth += 1
        elif character in "}]":
            depth -= 1
        elif character == "," and depth == 0:
            count += 1
    if depth != 0:
        raise ValueError("Malformed XML documentation parameter list.")
    return count


def flatten(node: ElementTree.Element | None) -> str:
    """Return one documentation node as a single line of text."""
    if node is None:
        return ""

    return " ".join("".join(node.itertext()).split())


def read_members(
    path: pathlib.Path,
    approved_members: frozenset[str],
) -> dict[str, str]:
    """Return each documented member identifier and its summary."""
    root = ElementTree.parse(path).getroot()
    members: dict[str, str] = {}
    for member in root.findall("./members/member"):
        name = member.get("name")
        if name is None or name not in approved_members:
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
        "Generated from compiler XML summaries and gated by the approved public",
        "contract, so documented internal helpers never render. Regenerate with",
        "`uv run python eng/docs/render_api_reference.py`.",
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
            lines.append(f"| {code_span(name[2:])} | {escaped} |")

    return "\n".join(lines) + "\n"


def code_span(value: str) -> str:
    """Render metadata names containing generic-arity backticks as valid Markdown."""
    longest_run = 0
    current_run = 0
    for character in value:
        current_run = current_run + 1 if character == "`" else 0
        longest_run = max(longest_run, current_run)
    delimiter = "`" * (longest_run + 1)
    return f"{delimiter}{value}{delimiter}"


def main(arguments: t.Sequence[str] | None = None) -> int:
    """Write the reference, or check the written one is current."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    parsed = parser.parse_args(arguments)

    paths = documentation_paths()
    if not paths:
        print("no built XML documentation found; build first", file=sys.stderr)
        return 1

    rendered = render(read_members(paths[0], public_member_ids(paths[0])))
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
