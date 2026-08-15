"""Prove the workflow check notices a workflow that stops covering the range."""

from __future__ import annotations

import pathlib
import runpy
import typing as t

import pytest


def load_checker() -> dict[str, t.Any]:
    """Load the workflow check as an import-free test namespace."""
    return runpy.run_path(
        str(pathlib.Path(__file__).parents[1] / "verify_workflows.py")
    )


SUPPORTED_TMUX_VERSIONS: tuple[str, ...] = load_checker()["SUPPORTED_TMUX_VERSIONS"]


def verify(root: pathlib.Path) -> list[str]:
    """Run the workflow check against one repository root."""
    checked: list[str] = load_checker()["verify"](root)
    return checked


BUILD = """
jobs:
  build:
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - run: dotnet restore --locked-mode
      - run: dotnet format --verify-no-changes
      - run: dotnet build --warnaserror
      - run: dotnet pack src/LibTmux/LibTmux.csproj
      - run: dotnet publish tests/LibTmux.AotSmoke/LibTmux.AotSmoke.csproj
      - run: dotnet run --project tests/LibTmux.PackageConsumer
      - run: dotnet run --project examples/LibTmux.Examples
"""

MATRIX = """
jobs:
  matrix:
    strategy:
      fail-fast: false
      matrix:
        tmux: [{versions}]
        framework: ['net8.0', 'net10.0']
    steps:
      - env:
          LIBTMUX_INTEGRATION_REQUIRED: '1'
        run: dotnet test
"""


def write(root: pathlib.Path, build: str, matrix: str) -> pathlib.Path:
    """Lay out a repository holding the two workflows."""
    workflows = root / ".github" / "workflows"
    workflows.mkdir(parents=True)
    (workflows / "dotnet.yml").write_text(build, encoding="utf-8")
    (workflows / "dotnet-tmux.yml").write_text(matrix, encoding="utf-8")
    return root


def every_version() -> str:
    """Return the matrix entry naming every supported tmux."""
    return ", ".join(f"'{version}'" for version in SUPPORTED_TMUX_VERSIONS)


def test_complete_workflows_pass(tmp_path: pathlib.Path) -> None:
    """A pair of workflows covering the whole range has nothing to report."""
    root = write(tmp_path, BUILD, MATRIX.format(versions=every_version()))

    assert verify(root) == []


def test_a_dropped_tmux_version_is_reported(tmp_path: pathlib.Path) -> None:
    """Quietly dropping a lane is exactly what this exists to catch."""
    versions = ", ".join(f"'{version}'" for version in SUPPORTED_TMUX_VERSIONS[:-1])
    root = write(tmp_path, BUILD, MATRIX.format(versions=versions))

    assert verify(root) == [f"dotnet-tmux.yml omits tmux {SUPPORTED_TMUX_VERSIONS[-1]}"]


def test_a_matrix_that_stops_early_is_reported(tmp_path: pathlib.Path) -> None:
    """One lane failing should not hide what the others would have said."""
    matrix = MATRIX.format(versions=every_version()).replace("fail-fast: false", "")
    root = write(tmp_path, BUILD, matrix)

    assert "dotnet-tmux.yml stops the matrix at the first failure" in verify(root)


def test_skipped_integration_tests_are_reported(tmp_path: pathlib.Path) -> None:
    """A lane whose tests skipped would pass while proving nothing."""
    matrix = MATRIX.format(versions=every_version()).replace(
        "LIBTMUX_INTEGRATION_REQUIRED", "SOMETHING_ELSE"
    )
    root = write(tmp_path, BUILD, matrix)

    assert "dotnet-tmux.yml does not require its integration tests to run" in verify(
        root
    )


@pytest.mark.parametrize(
    "step",
    ["--locked-mode", "--warnaserror", "dotnet pack", "LibTmux.PackageConsumer"],
)
def test_a_dropped_build_step_is_reported(tmp_path: pathlib.Path, step: str) -> None:
    """A workflow gating less than the repository does lets changes through."""
    root = write(
        tmp_path,
        BUILD.replace(step, "echo skipped"),
        MATRIX.format(versions=every_version()),
    )

    assert f"dotnet.yml omits {step}" in verify(root)


def test_a_missing_workflow_is_reported(tmp_path: pathlib.Path) -> None:
    """Deleting a workflow is the loudest way to stop testing."""
    root = write(tmp_path, BUILD, MATRIX.format(versions=every_version()))
    (root / ".github" / "workflows" / "dotnet-tmux.yml").unlink()

    assert verify(root) == ["missing workflow: dotnet-tmux.yml"]
