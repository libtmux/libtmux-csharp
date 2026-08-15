"""Read a built package the way a consumer's restore would.

Everything about a package is decided at pack time and invisible from inside
the repository: which frameworks it carries, what it drags in with it, and
whether the documentation a caller's editor shows is there at all.
"""

from __future__ import annotations

import argparse
import pathlib
import sys
import zipfile
from xml.etree import ElementTree

TARGET_FRAMEWORKS = ("net8.0", "net10.0")

#: Logging abstractions are interfaces with no implementation attached, so a
#: caller who wants no logging still pays nothing for it. Anything beyond this
#: would be the library choosing a caller's dependencies for them.
ALLOWED_DEPENDENCIES = frozenset({"Microsoft.Extensions.Logging.Abstractions"})


def inspect(package: pathlib.Path) -> list[str]:
    """Return one message per way the package falls short."""
    violations: list[str] = []
    with zipfile.ZipFile(package) as archive:
        names = set(archive.namelist())
        for framework in TARGET_FRAMEWORKS:
            if f"lib/{framework}/LibTmux.dll" not in names:
                violations.append(f"package carries no assembly for {framework}")

            # A caller's editor shows what the documentation file says, and a
            # package without one shows nothing.
            if f"lib/{framework}/LibTmux.xml" not in names:
                violations.append(f"package carries no documentation for {framework}")

        specifications = [name for name in names if name.endswith(".nuspec")]
        if len(specifications) != 1:
            violations.append("package does not carry exactly one specification")
            return violations

        with archive.open(specifications[0]) as stream:
            root = ElementTree.parse(stream).getroot()

    namespace = root.tag[: root.tag.index("}") + 1]
    metadata = root.find(f"{namespace}metadata")
    if metadata is None:
        violations.append("package specification carries no metadata")
        return violations

    violations.extend(
        f"package declares dependency {dependency.attrib['id']}"
        for dependency in root.iter(f"{namespace}dependency")
        if dependency.attrib.get("id") not in ALLOWED_DEPENDENCIES
    )

    for field, expected in (
        ("id", "LibTmux"),
        ("license", "MIT"),
        ("readme", "README.md"),
    ):
        element = metadata.find(f"{namespace}{field}")
        if element is None or element.text != expected:
            violations.append(f"package {field} is not {expected}")

    symbols = package.with_suffix(".snupkg")
    if not symbols.is_file():
        violations.append("no symbols package was produced beside the package")

    return violations


def main(argv: list[str] | None = None) -> int:
    """Report whether a built package is fit to publish."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--artifacts",
        type=pathlib.Path,
        default=pathlib.Path(__file__).resolve().parents[2] / "artifacts" / "packages",
        help="the directory dotnet pack wrote to",
    )
    parser.add_argument(
        "--repository",
        type=pathlib.Path,
        default=pathlib.Path(__file__).resolve().parents[3],
        help="the repository the package was built from",
    )
    arguments = parser.parse_args(argv)

    # The optional JSON package is inspected alongside the core one, because a
    # caller who takes it gets both and neither is checked by the other.
    found = sorted(arguments.artifacts.glob("LibTmux*.nupkg"))
    if not found:
        print(f"no package was found in {arguments.artifacts}")
        return 1

    violations = [message for package in found for message in inspect(package)]
    for violation in violations:
        print(violation)

    return 1 if violations else 0


if __name__ == "__main__":
    sys.exit(main())
