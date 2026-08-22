"""Prove the API reference contains only the approved public surface."""

from __future__ import annotations

import pathlib
import runpy
import typing as t

import pytest


def load_renderer() -> dict[str, t.Any]:
    """Load the renderer as an import-free test namespace."""
    return runpy.run_path(
        str(pathlib.Path(__file__).parents[1] / "render_api_reference.py")
    )


def test_member_reader_keeps_public_facade_and_drops_generated_types(
    tmp_path: pathlib.Path,
) -> None:
    """Compiler-generated documentation must not become package API docs."""
    documentation = tmp_path / "LibTmux.xml"
    documentation.write_text(
        """<?xml version="1.0"?>
<doc>
  <members>
    <member name="T:LibTmux.PsmuxConnectionOptions">
      <summary>Configures the preview.</summary>
    </member>
    <member name="P:LibTmux.PsmuxConnectionOptions.DataDirectory">
      <summary>Gets the data directory.</summary>
    </member>
    <member name="M:LibTmux.PsmuxConnectionOptions.ValidateInternalState">
      <summary>Must not expose an internal member of a public type.</summary>
    </member>
    <member name="T:LibTmux.Internal.HiddenType">
      <summary>Must not be published.</summary>
    </member>
    <member name="T:System.Text.RegularExpressions.Generated.VersionRegex_0">
      <summary>Must not be published.</summary>
    </member>
  </members>
</doc>
""",
        encoding="utf-8",
    )
    read_members = load_renderer()["read_members"]

    members = read_members(
        documentation,
        frozenset(
            {
                "T:LibTmux.PsmuxConnectionOptions",
                "P:LibTmux.PsmuxConnectionOptions.DataDirectory",
            }
        ),
    )

    assert members == {
        "T:LibTmux.PsmuxConnectionOptions": "Configures the preview.",
        "P:LibTmux.PsmuxConnectionOptions.DataDirectory": "Gets the data directory.",
    }


def test_contract_reader_excludes_internal_types(tmp_path: pathlib.Path) -> None:
    """The review contract may describe internals without publishing them."""
    contract = tmp_path / "public-api.json"
    contract.write_text(
        """{
  "types": [
    {
      "id": "T:LibTmux.PsmuxConnectionOptions",
      "package": "LibTmux",
      "modifiers": ["public", "sealed"]
    },
    {
      "id": "T:LibTmux.Internal.HiddenType",
      "package": "LibTmux",
      "modifiers": ["internal", "sealed"]
    },
    {
      "id": "T:LibTmux.Query.Json.QueryJson",
      "package": "LibTmux.Query.Json",
      "modifiers": ["public", "static"]
    }
  ],
  "members": [
    {
      "id": "P:LibTmux.PsmuxConnectionOptions.DataDirectory",
      "declaringType": "T:LibTmux.PsmuxConnectionOptions",
      "package": "LibTmux",
      "visibility": "public"
    },
    {
      "id": "M:LibTmux.PsmuxConnectionOptions.ValidateInternalState",
      "declaringType": "T:LibTmux.PsmuxConnectionOptions",
      "package": "LibTmux",
      "visibility": "internal"
    },
    {
      "id": "M:LibTmux.Internal.HiddenType.Escape",
      "declaringType": "T:LibTmux.Internal.HiddenType",
      "package": "LibTmux",
      "visibility": "public"
    }
  ]
}
""",
        encoding="utf-8",
    )
    public_type_names = load_renderer()["public_type_names"]

    assert public_type_names(contract) == frozenset({"LibTmux.PsmuxConnectionOptions"})



def test_contract_visibility_excludes_internal_members_of_public_types(
    tmp_path: pathlib.Path,
) -> None:
    """A documented helper is not public merely because its type is public."""
    documentation = tmp_path / "LibTmux.xml"
    documentation.write_text(
        """<?xml version="1.0"?>
<doc>
  <members>
    <member name="T:LibTmux.PsmuxConnectionOptions">
      <summary>Configures the preview.</summary>
    </member>
    <member name="P:LibTmux.PsmuxConnectionOptions.DataDirectory">
      <summary>Gets the data directory.</summary>
    </member>
    <member name="M:LibTmux.PsmuxConnectionOptions.ValidateInternalState">
      <summary>Must not expose an internal member.</summary>
    </member>
  </members>
</doc>
""",
        encoding="utf-8",
    )
    contract = tmp_path / "public-api.json"
    contract.write_text(
        """{
  "types": [
    {
      "id": "T:LibTmux.PsmuxConnectionOptions",
      "package": "LibTmux",
      "modifiers": ["public", "sealed"]
    }
  ],
  "members": [
    {
      "id": "P:LibTmux.PsmuxConnectionOptions.DataDirectory",
      "declaringType": "T:LibTmux.PsmuxConnectionOptions",
      "name": "DataDirectory",
      "kind": "property",
      "package": "LibTmux",
      "visibility": "public"
    },
    {
      "id": "M:LibTmux.PsmuxConnectionOptions.ValidateInternalState()",
      "declaringType": "T:LibTmux.PsmuxConnectionOptions",
      "name": "ValidateInternalState",
      "kind": "method",
      "parameters": [],
      "package": "LibTmux",
      "visibility": "internal"
    }
  ]
}
""",
        encoding="utf-8",
    )
    renderer = load_renderer()

    approved = renderer["public_member_ids"](documentation, contract)
    members = renderer["read_members"](documentation, approved)

    assert members == {
        "T:LibTmux.PsmuxConnectionOptions": "Configures the preview.",
        "P:LibTmux.PsmuxConnectionOptions.DataDirectory": "Gets the data directory.",
    }


def test_contract_mapping_fails_closed_on_an_unapproved_overload(
    tmp_path: pathlib.Path,
) -> None:
    """Name-and-arity collisions must stop generation instead of leaking a helper."""
    documentation = tmp_path / "LibTmux.xml"
    documentation.write_text(
        """<?xml version="1.0"?>
<doc>
  <members>
    <member name="M:LibTmux.PsmuxServer.Read(System.String)">
      <summary>Reads by name.</summary>
    </member>
    <member name="M:LibTmux.PsmuxServer.Read(System.Int32)">
      <summary>Internal numeric helper.</summary>
    </member>
  </members>
</doc>
""",
        encoding="utf-8",
    )
    contract = tmp_path / "public-api.json"
    contract.write_text(
        """{
  "types": [
    {
      "id": "T:LibTmux.PsmuxServer",
      "package": "LibTmux",
      "modifiers": ["public", "sealed"]
    }
  ],
  "members": [
    {
      "id": "M:LibTmux.PsmuxServer.Read(string)",
      "declaringType": "T:LibTmux.PsmuxServer",
      "name": "Read",
      "kind": "method",
      "parameters": [{"name": "name", "type": "string"}],
      "package": "LibTmux",
      "visibility": "public"
    }
  ]
}
""",
        encoding="utf-8",
    )

    with pytest.raises(ValueError, match="more members than the approved public shape"):
        load_renderer()["public_member_ids"](documentation, contract)


def test_member_reader_uses_one_canonical_type_summary(tmp_path: pathlib.Path) -> None:
    """A canonical partial-type summary is the summary readers receive."""
    documentation = tmp_path / "LibTmux.xml"
    documentation.write_text(
        """<?xml version="1.0"?>
<doc>
  <members>
    <member name="T:LibTmux.Server">
      <summary>Represents an immutable server handle and snapshot.</summary>
    </member>
  </members>
</doc>
""",
        encoding="utf-8",
    )
    read_members = load_renderer()["read_members"]

    members = read_members(documentation, frozenset({"T:LibTmux.Server"}))

    assert members == {
        "T:LibTmux.Server": "Represents an immutable server handle and snapshot."
    }


def test_renderer_preserves_generic_metadata_names_as_code() -> None:
    """Generic arity markers must not terminate their Markdown code span."""
    render = load_renderer()["render"]

    rendered = render(
        {"T:LibTmux.CapturedRelation`1": "Holds captured children."}
    )

    assert "| ``LibTmux.CapturedRelation`1`` | Holds captured children. |" in rendered

    rendered = render(
        {"M:LibTmux.Query.QueryExtensions.Compile``1": "Compiles a query."}
    )

    assert "| ```LibTmux.Query.QueryExtensions.Compile``1``` | Compiles a query. |" in rendered
