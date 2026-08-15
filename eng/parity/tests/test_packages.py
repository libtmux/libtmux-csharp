"""Prove the package check notices a package a consumer could not use."""

from __future__ import annotations

import pathlib
import runpy
import typing as t
import zipfile

import pytest

SPECIFICATION = """<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>LibTmux</id>
    <version>1.0.0</version>
    <description>A typed client for tmux.</description>
    <license type="expression">MIT</license>
    <readme>README.md</readme>
    <dependencies>
      <group targetFramework="net8.0">
        <dependency id="{dependency}" version="10.0.10" />
      </group>
    </dependencies>
  </metadata>
</package>
"""


def load_inspector() -> dict[str, t.Any]:
    """Load the package check as an import-free test namespace."""
    return runpy.run_path(
        str(pathlib.Path(__file__).parents[1] / "inspect_packages.py")
    )


def inspect(package: pathlib.Path) -> list[str]:
    """Run the package check against one built package."""
    found: list[str] = load_inspector()["inspect"](package)
    return found


def build(
    directory: pathlib.Path,
    *,
    frameworks: tuple[str, ...] = ("net8.0", "net10.0"),
    documentation: bool = True,
    dependency: str = "Microsoft.Extensions.Logging.Abstractions",
    symbols: bool = True,
) -> pathlib.Path:
    """Write a package shaped the way dotnet pack writes one."""
    package = directory / "LibTmux.1.0.0.nupkg"
    with zipfile.ZipFile(package, "w") as archive:
        archive.writestr("LibTmux.nuspec", SPECIFICATION.format(dependency=dependency))
        for framework in frameworks:
            archive.writestr(f"lib/{framework}/LibTmux.dll", "assembly")
            if documentation:
                archive.writestr(f"lib/{framework}/LibTmux.xml", "<doc />")

    if symbols:
        (directory / "LibTmux.1.0.0.snupkg").write_bytes(b"symbols")

    return package


def test_a_complete_package_passes(tmp_path: pathlib.Path) -> None:
    """A package carrying everything a consumer needs has nothing to report."""
    assert inspect(build(tmp_path)) == []


def test_a_missing_framework_is_reported(tmp_path: pathlib.Path) -> None:
    """A package that quietly stopped shipping a framework breaks a consumer."""
    package = build(tmp_path, frameworks=("net10.0",))

    assert "package carries no assembly for net8.0" in inspect(package)


def test_missing_documentation_is_reported(tmp_path: pathlib.Path) -> None:
    """A caller's editor shows the documentation file, or shows nothing."""
    package = build(tmp_path, documentation=False)

    assert "package carries no documentation for net8.0" in inspect(package)


def test_an_extra_dependency_is_reported(tmp_path: pathlib.Path) -> None:
    """Every dependency this takes is one a consumer is made to take too."""
    package = build(tmp_path, dependency="Newtonsoft.Json")

    assert "package declares dependency Newtonsoft.Json" in inspect(package)


def test_missing_symbols_are_reported(tmp_path: pathlib.Path) -> None:
    """Stepping into the library while debugging needs the symbols."""
    package = build(tmp_path, symbols=False)

    assert "no symbols package was produced beside the package" in inspect(package)


@pytest.mark.parametrize(
    ("field", "replacement"),
    [("MIT", "Apache-2.0"), ("README.md", "docs.md")],
)
def test_changed_metadata_is_reported(
    tmp_path: pathlib.Path,
    field: str,
    replacement: str,
) -> None:
    """The package page says what this is and who may use it, or it does not."""
    package = tmp_path / "LibTmux.1.0.0.nupkg"
    with zipfile.ZipFile(package, "w") as archive:
        archive.writestr(
            "LibTmux.nuspec",
            SPECIFICATION.format(
                dependency="Microsoft.Extensions.Logging.Abstractions"
            ).replace(field, replacement),
        )
        for framework in ("net8.0", "net10.0"):
            archive.writestr(f"lib/{framework}/LibTmux.dll", "assembly")
            archive.writestr(f"lib/{framework}/LibTmux.xml", "<doc />")

    (tmp_path / "LibTmux.1.0.0.snupkg").write_bytes(b"symbols")

    assert inspect(package) != []
