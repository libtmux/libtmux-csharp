"""Tests for the behavioral RED evidence runner."""

from __future__ import annotations

import json
import os
import pathlib
import stat
import subprocess
import sys
import typing as t
import xml.etree.ElementTree as et

import pytest

RUNNER = pathlib.Path(__file__).parents[1] / "require_red.py"
SELECTED_TEST = (
    "LibTmux.UnitTests.Transport.TmuxProcessTransportTests."
    "Preserves_raw_bytes_and_projects_universal_newlines"
)
UNEXPECTED_TEST = "LibTmux.UnitTests.Transport.OtherTests.Fails"
TRX_NAMESPACE = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"


def trx_name(local_name: str) -> str:
    """Return a namespace-qualified TRX element name."""
    return f"{{{TRX_NAMESPACE}}}{local_name}"


def trx_document(
    results: t.Sequence[tuple[str, str]],
    *,
    summary_outcome: str,
    executed: int,
    failed: int,
    passed: int = 0,
    aborted: int = 0,
    not_executed: int = 0,
) -> str:
    """Build a minimal, linked TRX document for one runner scenario."""
    et.register_namespace("", TRX_NAMESPACE)
    root = et.Element(trx_name("TestRun"), id="run-id")
    trx_results = et.SubElement(root, trx_name("Results"))
    definitions = et.SubElement(root, trx_name("TestDefinitions"))
    for index, (identity, outcome) in enumerate(results):
        test_id = f"test-{index}"
        et.SubElement(
            trx_results,
            trx_name("UnitTestResult"),
            testId=test_id,
            testName=identity,
            outcome=outcome,
        )
        definition = et.SubElement(
            definitions,
            trx_name("UnitTest"),
            id=test_id,
            name=identity,
        )
        class_name, method_name = identity.rsplit(".", 1)
        et.SubElement(
            definition,
            trx_name("TestMethod"),
            className=class_name,
            name=method_name,
        )

    summary = et.SubElement(
        root,
        trx_name("ResultSummary"),
        outcome=summary_outcome,
    )
    et.SubElement(
        summary,
        trx_name("Counters"),
        total=str(len(results)),
        executed=str(executed),
        passed=str(passed),
        failed=str(failed),
        error="0",
        timeout="0",
        aborted=str(aborted),
        inconclusive="0",
        passedButRunAborted="0",
        notRunnable="0",
        notExecuted=str(not_executed),
        disconnected="0",
        warning="0",
        completed="0",
        inProgress="0",
        pending="0",
    )
    return et.tostring(root, encoding="unicode", xml_declaration=True)


def install_fake_command(tmp_path: pathlib.Path, name: str) -> pathlib.Path:
    """Install a deterministic fake SDK launcher and return its directory."""
    executable = tmp_path / "bin" / name
    executable.parent.mkdir(exist_ok=True)
    executable.write_text(
        f"""#!{sys.executable}
import json
import os
import pathlib
import sys

arguments = sys.argv[1:]
pathlib.Path(os.environ["FAKE_DOTNET_INVOCATION"]).write_text(
    json.dumps(arguments), encoding="utf-8"
)
dotnet_arguments = arguments
if arguments[:3] == ["exec", "--", "dotnet"]:
    dotnet_arguments = arguments[3:]
elif arguments[:2] == ["exec", "--cd"]:
    dotnet_arguments = arguments[5:]
if os.environ["FAKE_DOTNET_WRITE_TRX"] == "1":
    if "--report-xunit-trx-filename" in dotnet_arguments:
        report_filename = pathlib.Path(
            dotnet_arguments[
                dotnet_arguments.index("--report-xunit-trx-filename") + 1
            ]
        )
        if "--results-directory" in dotnet_arguments:
            results_directory = pathlib.Path(
                dotnet_arguments[dotnet_arguments.index("--results-directory") + 1]
            )
            evidence = results_directory / report_filename
        else:
            evidence = report_filename
    else:
        results_directory = pathlib.Path(
            dotnet_arguments[dotnet_arguments.index("--results-directory") + 1]
        )
        logger = dotnet_arguments[dotnet_arguments.index("--logger") + 1]
        log_file_name = logger.split("LogFileName=", 1)[1]
        evidence = results_directory / log_file_name
    evidence.parent.mkdir(parents=True, exist_ok=True)
    evidence.write_text(os.environ["FAKE_DOTNET_TRX"], encoding="utf-8")
print(os.environ.get("FAKE_DOTNET_OUTPUT", ""), file=sys.stderr)
raise SystemExit(int(os.environ["FAKE_DOTNET_EXIT_CODE"]))
""",
        encoding="utf-8",
        newline="\n",
    )
    executable.chmod(
        executable.stat().st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH
    )
    return executable.parent


def invoke_runner(
    tmp_path: pathlib.Path,
    *,
    trx: str | None,
    process_exit_code: int,
    process_output: str = "",
    available_commands: tuple[str, ...] = ("dotnet", "mise"),
    relative_paths: bool = False,
) -> tuple[subprocess.CompletedProcess[str], list[str], pathlib.Path]:
    """Invoke the runner through real child processes with fake SDK tools."""
    for command in available_commands:
        fake_bin = install_fake_command(tmp_path, command)
    invocation_path = tmp_path / "dotnet-argv.json"
    evidence_path = tmp_path / "evidence" / "component-01.trx"
    project_path = tmp_path / "LibTmux.UnitTests.csproj"
    project_argument = (
        pathlib.Path(project_path.name) if relative_paths else project_path
    )
    evidence_argument = (
        evidence_path.relative_to(tmp_path) if relative_paths else evidence_path
    )
    environment = os.environ.copy()
    environment.update(
        {
            "FAKE_DOTNET_EXIT_CODE": str(process_exit_code),
            "FAKE_DOTNET_INVOCATION": str(invocation_path),
            "FAKE_DOTNET_OUTPUT": process_output,
            "FAKE_DOTNET_TRX": trx or "",
            "FAKE_DOTNET_WRITE_TRX": "1" if trx is not None else "0",
            "PATH": str(fake_bin),
        }
    )
    completed = subprocess.run(
        [
            sys.executable,
            str(RUNNER),
            "--project",
            str(project_argument),
            "--configuration",
            "Release",
            "--framework",
            "net8.0",
            "--no-restore",
            "--test",
            SELECTED_TEST,
            "--evidence",
            str(evidence_argument),
        ],
        check=False,
        capture_output=True,
        cwd=tmp_path if relative_paths else None,
        env=environment,
        text=True,
    )
    invocation = (
        t.cast(list[str], json.loads(invocation_path.read_text(encoding="utf-8")))
        if invocation_path.exists()
        else []
    )
    return completed, invocation, evidence_path


def assert_red_rejected(completed: subprocess.CompletedProcess[str]) -> None:
    """Assert that the runner rejected evidence for a contract reason."""
    assert completed.returncode != 0
    assert "RED rejected:" in completed.stderr


def expected_dotnet_arguments(
    tmp_path: pathlib.Path,
    evidence: pathlib.Path,
) -> list[str]:
    """Return the frozen Microsoft Testing Platform argument vector."""
    return [
        "test",
        "--project",
        str(tmp_path / "LibTmux.UnitTests.csproj"),
        "--configuration",
        "Release",
        "--framework",
        "net8.0",
        "--no-restore",
        "--filter-method",
        SELECTED_TEST,
        "--results-directory",
        str(evidence.parent),
        "--report-xunit-trx",
        "--report-xunit-trx-filename",
        evidence.name,
    ]


def test_accepts_only_nonzero_run_with_exact_failed_selected_test(
    tmp_path: pathlib.Path,
) -> None:
    """Accept one fresh selected failure and invoke one exact test filter."""
    trx = trx_document(
        [(SELECTED_TEST, "Failed")],
        summary_outcome="Failed",
        executed=1,
        failed=1,
    )

    completed, invocation, evidence = invoke_runner(
        tmp_path,
        trx=trx,
        process_exit_code=1,
    )

    assert completed.returncode == 0, completed.stderr
    assert completed.stdout == f"RED confirmed: {SELECTED_TEST}\n"
    assert invocation == expected_dotnet_arguments(tmp_path, evidence)
    assert evidence.read_text(encoding="utf-8") == trx


def test_falls_back_to_repository_mise_when_dotnet_is_absent(
    tmp_path: pathlib.Path,
) -> None:
    """Use repository mise without weakening the exact dotnet arguments."""
    trx = trx_document(
        [(SELECTED_TEST, "Failed")],
        summary_outcome="Failed",
        executed=1,
        failed=1,
    )

    completed, invocation, evidence = invoke_runner(
        tmp_path,
        trx=trx,
        process_exit_code=1,
        available_commands=("mise",),
        relative_paths=True,
    )

    assert completed.returncode == 0, completed.stderr
    assert invocation == [
        "exec",
        "--cd",
        str(RUNNER.parents[2].resolve()),
        "--",
        "dotnet",
        *expected_dotnet_arguments(tmp_path, evidence),
    ]


@pytest.mark.parametrize(
    "failure_output",
    ("Build FAILED.", "No test matches the given testcase filter."),
)
def test_rejects_build_or_discovery_failure(
    tmp_path: pathlib.Path,
    failure_output: str,
) -> None:
    """Reject a failed process that did not execute a behavioral test."""
    completed, _, evidence = invoke_runner(
        tmp_path,
        trx=None,
        process_exit_code=1,
        process_output=failure_output,
    )

    assert_red_rejected(completed)
    assert not evidence.exists()


@pytest.mark.parametrize(
    "trx",
    (
        trx_document(
            [],
            summary_outcome="Completed",
            executed=0,
            failed=0,
        ),
        trx_document(
            [(SELECTED_TEST, "NotExecuted")],
            summary_outcome="Completed",
            executed=0,
            failed=0,
            not_executed=1,
        ),
    ),
    ids=("zero-tests", "all-skipped"),
)
def test_rejects_zero_tests_and_all_skipped_tests(
    tmp_path: pathlib.Path,
    trx: str,
) -> None:
    """Reject runs without an executed selected test."""
    completed, _, evidence = invoke_runner(
        tmp_path,
        trx=trx,
        process_exit_code=1,
    )

    assert_red_rejected(completed)
    assert not evidence.exists()


@pytest.mark.parametrize("summary_outcome", ("Aborted", "Canceled"))
def test_rejects_aborted_or_canceled_runs(
    tmp_path: pathlib.Path,
    summary_outcome: str,
) -> None:
    """Reject incomplete runs even if their selected result says Failed."""
    trx = trx_document(
        [(SELECTED_TEST, "Failed")],
        summary_outcome=summary_outcome,
        executed=1,
        failed=1,
        aborted=1,
    )

    completed, _, evidence = invoke_runner(
        tmp_path,
        trx=trx,
        process_exit_code=1,
    )

    assert_red_rejected(completed)
    assert not evidence.exists()


@pytest.mark.parametrize("trx", ("<TestRun", None), ids=("malformed", "missing"))
def test_rejects_malformed_or_missing_trx(
    tmp_path: pathlib.Path,
    trx: str | None,
) -> None:
    """Reject absent evidence and XML that is not a complete TRX document."""
    completed, _, evidence = invoke_runner(
        tmp_path,
        trx=trx,
        process_exit_code=1,
    )

    assert_red_rejected(completed)
    assert not evidence.exists()


@pytest.mark.parametrize(
    "results",
    (
        ((UNEXPECTED_TEST, "Failed"),),
        ((SELECTED_TEST, "Failed"), (SELECTED_TEST, "Failed")),
    ),
    ids=("unexpected", "duplicate-selected"),
)
def test_rejects_unexpected_test_identity(
    tmp_path: pathlib.Path,
    results: tuple[tuple[str, str], ...],
) -> None:
    """Reject evidence that is not exactly one selected test result."""
    trx = trx_document(
        results,
        summary_outcome="Failed",
        executed=len(results),
        failed=len(results),
    )

    completed, _, evidence = invoke_runner(
        tmp_path,
        trx=trx,
        process_exit_code=1,
    )

    assert_red_rejected(completed)
    assert not evidence.exists()


@pytest.mark.parametrize(
    ("outcome", "process_exit_code", "summary_outcome", "failed", "passed"),
    (
        ("Passed", 0, "Completed", 0, 1),
        ("Failed", 0, "Failed", 1, 0),
    ),
    ids=("passing-test", "zero-exit-with-failed-trx"),
)
def test_rejects_successful_test_run(
    tmp_path: pathlib.Path,
    outcome: str,
    process_exit_code: int,
    summary_outcome: str,
    failed: int,
    passed: int,
) -> None:
    """Reject a successful test process regardless of its TRX claims."""
    trx = trx_document(
        [(SELECTED_TEST, outcome)],
        summary_outcome=summary_outcome,
        executed=1,
        failed=failed,
        passed=passed,
    )

    completed, _, evidence = invoke_runner(
        tmp_path,
        trx=trx,
        process_exit_code=process_exit_code,
    )

    assert_red_rejected(completed)
    assert not evidence.exists()


def test_rejects_stale_exact_failed_trx_after_build_or_discovery_failure(
    tmp_path: pathlib.Path,
) -> None:
    """Delete stale valid evidence before a failed dotnet invocation."""
    stale = trx_document(
        [(SELECTED_TEST, "Failed")],
        summary_outcome="Failed",
        executed=1,
        failed=1,
    )
    evidence = tmp_path / "evidence" / "component-01.trx"
    evidence.parent.mkdir()
    evidence.write_text(stale, encoding="utf-8")

    completed, _, returned_evidence = invoke_runner(
        tmp_path,
        trx=None,
        process_exit_code=1,
        process_output="Build FAILED.",
    )

    assert returned_evidence == evidence
    assert_red_rejected(completed)
    assert not evidence.exists()
