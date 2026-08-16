"""Read the pinned Python libtmux revision the parity records are built from.

The inventory, the ledger and every error policy quote one revision of Python
libtmux. That history used to sit in the same repository as this project and
now does not, so the checkout holding it is named rather than assumed: a
missing one has to say what to do about it instead of reading as a record that
failed to validate.
"""

from __future__ import annotations

import functools
import os
import pathlib
import subprocess

#: The Python libtmux revision every parity record is grounded in.
REVISION = "c4a980b"
#: Where a reader follows a record back to the source it came from.
BLOB_URL_PREFIX = f"https://github.com/tmux-python/libtmux/blob/{REVISION}/"
#: Names the checkout that holds :data:`REVISION`.
REPOSITORY_VARIABLE = "LIBTMUX_PYTHON_REPOSITORY"


def candidates() -> list[pathlib.Path]:
    """Return the checkouts that could hold the pinned revision, in order.

    Returns
    -------
    list[pathlib.Path]
        Directories to try, most specific first.

    Examples
    --------
    >>> all(isinstance(candidate, pathlib.Path) for candidate in candidates())
    True
    """
    configured = os.environ.get(REPOSITORY_VARIABLE)
    if configured:
        return [pathlib.Path(configured).expanduser()]

    # A sibling checkout is the layout that needs no configuration at all.
    return [pathlib.Path(__file__).resolve().parents[2].parent / "libtmux"]


def holds_revision(repository: pathlib.Path) -> bool:
    """Return whether a checkout can resolve the pinned revision.

    Parameters
    ----------
    repository : pathlib.Path
        Directory to ask.

    Returns
    -------
    bool
        Whether ``git`` resolves the revision to a commit there.

    Examples
    --------
    >>> holds_revision(pathlib.Path("/nonexistent"))
    False
    """
    return (
        subprocess.run(
            ["git", "-C", str(repository), "cat-file", "-e", f"{REVISION}^{{commit}}"],
            check=False,
            capture_output=True,
        ).returncode
        == 0
    )


@functools.cache
def repository() -> pathlib.Path:
    """Return the checkout holding the pinned Python libtmux revision.

    Returns
    -------
    pathlib.Path
        Directory a ``git show`` of the pinned revision resolves in.

    Raises
    ------
    SystemExit
        No candidate checkout has the revision.
    """
    tried = candidates()
    for candidate in tried:
        if holds_revision(candidate):
            return candidate

    raise SystemExit(
        f"These records are grounded in Python libtmux {REVISION}, which none of "
        f"{', '.join(str(candidate) for candidate in tried)} has. Point "
        f"{REPOSITORY_VARIABLE} at a checkout of "
        "https://github.com/tmux-python/libtmux that contains it.",
    )


def show(path: str) -> str:
    """Return one path as the pinned revision has it.

    Parameters
    ----------
    path : str
        Path relative to the Python libtmux repository root.

    Returns
    -------
    str
        File contents at the pinned revision.

    Examples
    --------
    >>> "raise_if_stderr" in show("src/libtmux/common.py")
    True
    """
    return subprocess.run(
        ["git", "-C", str(repository()), "show", f"{REVISION}:{path}"],
        check=True,
        stdout=subprocess.PIPE,
        text=True,
    ).stdout
