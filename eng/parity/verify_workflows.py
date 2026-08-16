"""Check the CI workflows test everything the library claims to support.

The README and the package name the tmux versions and target frameworks this
works on. A workflow that covers fewer turns that claim into something nobody
checks, and the gap is invisible until a user on the missing version reports
it.
"""

from __future__ import annotations

import argparse
import pathlib
import sys

SUPPORTED_TMUX_VERSIONS = ("3.2a", "3.3a", "3.4", "3.5", "3.6", "3.7a", "3.7b")
TARGET_FRAMEWORKS = ("net8.0", "net10.0")

#: Checks a change has to pass locally. A workflow missing one of these would
#: let a change through that the repository itself would refuse.
REQUIRED_BUILD_STEPS = (
    "--locked-mode",
    "--verify-no-changes",
    "--warnaserror",
    "dotnet pack",
    "LibTmux.AotSmoke",
    "LibTmux.PackageConsumer",
    "LibTmux.Examples",
    "LibTmux.ExampleTests",
    "sync_snippets.py --check",
    "fetch-depth: 0",
)


def verify(root: pathlib.Path) -> list[str]:
    """Return one message per way the workflows fall short."""
    violations: list[str] = []
    workflows = root / ".github" / "workflows"

    build = workflows / "dotnet.yml"
    matrix = workflows / "dotnet-tmux.yml"
    violations.extend(
        f"missing workflow: {path.name}"
        for path in (build, matrix)
        if not path.is_file()
    )

    if violations:
        return violations

    build_text = build.read_text(encoding="utf-8")
    matrix_text = matrix.read_text(encoding="utf-8")

    violations.extend(
        f"dotnet.yml omits {step}"
        for step in REQUIRED_BUILD_STEPS
        if step not in build_text
    )
    violations.extend(
        f"dotnet-tmux.yml omits tmux {version}"
        for version in SUPPORTED_TMUX_VERSIONS
        if f"'{version}'" not in matrix_text
    )
    violations.extend(
        f"dotnet-tmux.yml omits {framework}"
        for framework in TARGET_FRAMEWORKS
        if f"'{framework}'" not in matrix_text
    )

    # One lane failing says something about that tmux version, which is only
    # readable when the other lanes still run.
    if "fail-fast: false" not in matrix_text:
        violations.append("dotnet-tmux.yml stops the matrix at the first failure")

    # A lane whose integration tests silently skipped would pass while proving
    # nothing at all.
    if "LIBTMUX_INTEGRATION_REQUIRED" not in matrix_text:
        violations.append(
            "dotnet-tmux.yml does not require its integration tests to run"
        )

    return violations


def main(argv: list[str] | None = None) -> int:
    """Report whether the workflows cover what is claimed."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--root",
        type=pathlib.Path,
        default=pathlib.Path(__file__).resolve().parents[2],
        help="the repository root holding .github/workflows",
    )
    arguments = parser.parse_args(argv)
    violations = verify(arguments.root)
    for violation in violations:
        print(violation)

    return 1 if violations else 0


if __name__ == "__main__":
    sys.exit(main())
