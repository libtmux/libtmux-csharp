"""Require one fresh, behavioral dotnet-test failure before implementation."""

from __future__ import annotations

import argparse
import pathlib
import shutil
import subprocess
import sys
import typing as t
import xml.etree.ElementTree as et

TRX_NAMESPACE = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"
CSHARP_ROOT = pathlib.Path(__file__).resolve().parents[2]


class RedEvidenceError(RuntimeError):
    """Report that a dotnet test invocation did not prove behavioral RED."""


def reject(message: str) -> t.NoReturn:
    """Raise one consistently typed RED evidence failure.

    Parameters
    ----------
    message : str
        Concrete reason the receipt cannot be accepted.
    """
    raise RedEvidenceError(message)


def trx_name(local_name: str) -> str:
    """Return a namespace-qualified TRX element name.

    Parameters
    ----------
    local_name : str
        Element name without a namespace.

    Returns
    -------
    str
        Qualified element name.

    Examples
    --------
    >>> trx_name("TestRun")
    '{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}TestRun'
    """
    return f"{{{TRX_NAMESPACE}}}{local_name}"


def dotnet_launcher() -> list[str]:
    """Select a direct SDK or the repository mise fallback.

    Returns
    -------
    list[str]
        Process argument prefix used to invoke dotnet.

    Raises
    ------
    RedEvidenceError
        Neither dotnet nor mise is available on PATH.
    """
    if shutil.which("dotnet") is not None:
        return ["dotnet"]
    if shutil.which("mise") is not None:
        return ["mise", "exec", "--cd", str(CSHARP_ROOT), "--", "dotnet"]
    reject("neither dotnet nor mise is available on PATH")


def dotnet_test_command(
    *,
    project: pathlib.Path,
    configuration: str,
    framework: str,
    test_identity: str,
    evidence: pathlib.Path,
) -> list[str]:
    """Build the exact filtered dotnet test invocation.

    Parameters
    ----------
    project : pathlib.Path
        Test project to execute.
    configuration : str
        Build configuration to consume.
    framework : str
        Target framework to execute.
    test_identity : str
        Fully qualified test identity selected for RED.
    evidence : pathlib.Path
        Exact TRX evidence destination.

    Returns
    -------
    list[str]
        Process argument vector.
    """
    absolute_evidence = evidence.resolve()
    return [
        *dotnet_launcher(),
        "test",
        "--project",
        str(project.resolve()),
        "--configuration",
        configuration,
        "--framework",
        framework,
        "--no-restore",
        "--filter-method",
        test_identity,
        "--results-directory",
        str(absolute_evidence.parent),
        "--report-xunit-trx",
        "--report-xunit-trx-filename",
        absolute_evidence.name,
    ]


def counter_value(counters: et.Element, name: str) -> int:
    """Read one required nonnegative TRX counter.

    Parameters
    ----------
    counters : xml.etree.ElementTree.Element
        TRX Counters element.
    name : str
        Counter attribute name.

    Returns
    -------
    int
        Parsed counter.

    Raises
    ------
    RedEvidenceError
        The counter is absent, malformed, or negative.
    """
    raw_value = counters.get(name)
    try:
        value = int(raw_value) if raw_value is not None else -1
    except ValueError:
        reject(f"TRX counter {name!r} is malformed")
    if value < 0:
        reject(f"TRX counter {name!r} is missing or negative")
    return value


def definition_identity(definition: et.Element) -> str:
    """Return the fully qualified identity linked from a TRX definition.

    Parameters
    ----------
    definition : xml.etree.ElementTree.Element
        UnitTest definition.

    Returns
    -------
    str
        Fully qualified class and method identity.

    Raises
    ------
    RedEvidenceError
        The definition is missing its TestMethod identity.
    """
    methods = definition.findall(f"./{trx_name('TestMethod')}")
    if len(methods) != 1:
        reject("TRX test definition is missing one TestMethod")
    class_name = methods[0].get("className")
    method_name = methods[0].get("name")
    if not class_name or not method_name:
        reject("TRX TestMethod identity is incomplete")
    return f"{class_name}.{method_name}"


def validate_trx(evidence: pathlib.Path, test_identity: str) -> None:
    """Validate that one fresh TRX proves the selected test failed.

    Parameters
    ----------
    evidence : pathlib.Path
        Newly created TRX path.
    test_identity : str
        Fully qualified test identity required to fail.

    Raises
    ------
    RedEvidenceError
        The document does not prove one complete selected test failure.
    """
    if not evidence.is_file():
        reject("fresh TRX evidence is missing")
    try:
        root = et.parse(evidence).getroot()
    except (OSError, et.ParseError):
        reject("fresh TRX evidence is malformed")
    if root.tag != trx_name("TestRun"):
        reject("evidence is not a TRX TestRun document")

    summaries = root.findall(f"./{trx_name('ResultSummary')}")
    if len(summaries) != 1 or summaries[0].get("outcome") != "Failed":
        reject("TRX run did not finish with outcome Failed")
    counters_elements = summaries[0].findall(f"./{trx_name('Counters')}")
    if len(counters_elements) != 1:
        reject("TRX run is missing one Counters element")
    counters = counters_elements[0]
    expected_counters = {
        "total": 1,
        "executed": 1,
        "passed": 0,
        "failed": 1,
        "aborted": 0,
        "passedButRunAborted": 0,
        "notExecuted": 0,
    }
    for name, expected in expected_counters.items():
        if counter_value(counters, name) != expected:
            reject(f"TRX counter {name!r} does not prove one complete failure")

    results = root.findall(f"./{trx_name('Results')}/{trx_name('UnitTestResult')}")
    if len(results) != 1:
        reject("TRX must contain exactly one test result")
    result = results[0]
    if result.get("testName") != test_identity:
        reject("TRX contains an unexpected test identity")
    if result.get("outcome") != "Failed":
        reject("selected test outcome is not Failed")

    definitions = root.findall(
        f"./{trx_name('TestDefinitions')}/{trx_name('UnitTest')}"
    )
    if len(definitions) != 1:
        reject("TRX must contain exactly one test definition")
    definition = definitions[0]
    if definition.get("name") != test_identity:
        reject("TRX definition contains an unexpected identity")
    if definition_identity(definition) != test_identity:
        reject("TRX TestMethod contains an unexpected identity")
    if not result.get("testId") or result.get("testId") != definition.get("id"):
        reject("TRX result is not linked to its test definition")


def discard_evidence(evidence: pathlib.Path) -> None:
    """Remove an evidence path without accepting stale or invalid content.

    Parameters
    ----------
    evidence : pathlib.Path
        TRX path to remove.

    Raises
    ------
    RedEvidenceError
        The evidence path cannot be removed.
    """
    try:
        evidence.unlink(missing_ok=True)
    except OSError:
        reject("TRX evidence path cannot be removed")


def require_red(
    *,
    project: pathlib.Path,
    configuration: str,
    framework: str,
    test_identity: str,
    evidence: pathlib.Path,
) -> None:
    """Execute one test and require a fresh, exact failed-test receipt.

    Parameters
    ----------
    project : pathlib.Path
        Test project to execute.
    configuration : str
        Build configuration to consume.
    framework : str
        Target framework to execute.
    test_identity : str
        Fully qualified selected behavioral test.
    evidence : pathlib.Path
        TRX path replaced by this invocation.

    Raises
    ------
    RedEvidenceError
        The process or TRX does not prove behavioral RED.
    """
    discard_evidence(evidence)
    evidence.parent.mkdir(parents=True, exist_ok=True)
    command = dotnet_test_command(
        project=project,
        configuration=configuration,
        framework=framework,
        test_identity=test_identity,
        evidence=evidence,
    )
    try:
        completed = subprocess.run(command, check=False)
    except OSError:
        reject("dotnet test could not be started")
    if completed.returncode == 0:
        reject("dotnet test succeeded")
    if completed.returncode < 0:
        reject("dotnet test was terminated")
    validate_trx(evidence, test_identity)


def parse_args(argv: t.Sequence[str] | None = None) -> argparse.Namespace:
    """Parse one strict RED invocation.

    Parameters
    ----------
    argv : Sequence[str] | None
        Optional command arguments.

    Returns
    -------
    argparse.Namespace
        Parsed runner arguments.
    """
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", required=True, type=pathlib.Path)
    parser.add_argument("--configuration", required=True)
    parser.add_argument("--framework", required=True)
    parser.add_argument("--no-restore", action="store_true", required=True)
    parser.add_argument("--test", required=True)
    parser.add_argument("--evidence", required=True, type=pathlib.Path)
    return parser.parse_args(argv)


def main(argv: t.Sequence[str] | None = None) -> int:
    """Run the strict RED gate.

    Parameters
    ----------
    argv : Sequence[str] | None
        Optional command arguments.

    Returns
    -------
    int
        Zero only when fresh TRX evidence proves behavioral RED.
    """
    arguments = parse_args(argv)
    try:
        require_red(
            project=arguments.project,
            configuration=arguments.configuration,
            framework=arguments.framework,
            test_identity=arguments.test,
            evidence=arguments.evidence,
        )
    except RedEvidenceError as exception:
        try:
            discard_evidence(arguments.evidence)
        except RedEvidenceError as cleanup_exception:
            print(
                f"RED rejected: {exception}; {cleanup_exception}",
                file=sys.stderr,
            )
            return 1
        print(f"RED rejected: {exception}", file=sys.stderr)
        return 1
    print(f"RED confirmed: {arguments.test}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
