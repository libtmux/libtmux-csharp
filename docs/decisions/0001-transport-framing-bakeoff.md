# ADR 0001: One-shot raw-byte tmux transport

## Status

Accepted for the first production transport.

## Context

The bakeoff compared three internal transport shapes against one shared workload
and oracle: a one-shot raw-byte pump, a one-shot line/event reader, and a
serialized persistent control client. The evaluated tree is
`d1a018074cdfc5ca7408c75f7161245f4ae8363d` with source fingerprint
`ee990952d56320e398b10c5b9490081472aac66bf7d4f5fa5b21a9a138efaeb1`.

The retained matrix ran on Linux with .NET SDK 10.0.302, `net8.0` and
`net10.0`, tmux 3.2a through 3.7b, and advisory tmux master. The advisory lanes
inform version differences but do not determine the winner.

Hard-gate evaluation preceded comparative scoring. Unsupported requirements
were rejected before process start, so a contender was not credited for a
workload it could not preserve.

| Requirement | Raw-byte pump | Line/event | Persistent control |
| --- | --- | --- | --- |
| Embedded newlines | Supported | Unsupported | Unsupported |
| Partial final line | Supported | Supported | Unsupported |
| Raw-byte fidelity | Supported | Unsupported | Unsupported |
| Exact invalid-byte projection | Supported | Unsupported | Unsupported |

## Decision

Use an internal one-shot process transport with concurrent raw stdout and stderr
capture. Retain bytes through byte-length framing. Decode framed fields with
deterministic lowercase `\xNN` replacement for each invalid byte. Keep normalized
line views as projections above the authoritative raw result.

Build tmux invocations with `ProcessStartInfo.ArgumentList`; never use a shell
command string. Model independent requests separately from a structural tmux
semicolon group. A failed independent request does not suppress a later request.
tmux may suppress a semicolon-group suffix, and a one-shot failure without
per-member evidence leaves every member outcome unknown.

A nonzero tmux exit remains an inspectable result containing argv, exit code,
stdout, and stderr. Higher layers own command-specific behavior such as
`has-session`, lenient list accessors, liveness checks, and warning projection.

Cancellation before process start uses the caller token and has no side effect.
After a successful start, cancellation reports that the command may have run,
kills and reaps only the client PID with a fresh bounded cleanup budget, settles
both output streams, and never kills a whole process tree.

Persistent control is not a transparent default. It creates an observable
attached client, changes attachment-dependent selection, requires correlation
state and tombstones. Stable tmux 3.2a through 3.7b can emit a self-targeted
`refresh-client` pause or continue line inside that command's guard.
Separately, sentinel-shaped user output can collide with reply correlation. A
future control engine must be an explicit capability-bearing opt-in.

## Matrix observations

Every required lane passed 139 tests with zero skips. Both advisory master lanes
also passed 139 tests with zero skips.

| tmux | Source commit | `net8.0` | `net10.0` | Role |
| --- | --- | --- | --- | --- |
| 3.2a | `3b929f332aafa7f1080eacc31feb11ffbb1d1841` | Passed | Passed | Required |
| 3.3a | `0b355ae8114511e1ff6359272b164f1cdf718e80` | Passed | Passed | Required |
| 3.4 | `9ae69c3795ab5ef6b4d760f6398cd9281151f632` | Passed | Passed | Required |
| 3.5 | `ac44566c9c7e3e94d23be6def4c7ae83472543f5` | Passed | Passed | Required |
| 3.6 | `0dac7fe434d029a4f0b819cba8eb7963df291990` | Passed | Passed | Required |
| 3.7a | `0e418b62d259ce8da8970f75732cc6632ee4c3a0` | Passed | Passed | Required |
| 3.7b | `e802909de06012a4df6209d55e86487c56223163` | Passed | Passed | Required |
| master | `851c5a933d4838c32ad06c248b2ba975d106149c` | Passed | Passed | Advisory |

The semantic transcripts separately prove, for each required lane, that written
control cancellation drains its tombstone before the next request, a failed
middle semicolon member suppresses the suffix, and an attached control client is
observable and restores server state after disposal.

## Production grafts

The production transport must add the following behavior without copying a
contender wholesale:

- Complete connection options for socket paths and names, socket-name factory
  precedence, configuration, color mode, initialization callback, child
  environment, and inherited `TMUX` removal, with real `-S` and `-L` tests.
- Python-compatible stdout and stderr line projection, `has-session` projection,
  lenient list policy, explicit liveness policy, and warning handling above raw
  transport results.
- Configured ceilings for workload members, captured stream bytes, and framed
  field bytes. A future control opt-in also requires control-block and
  diagnostic-event ceilings.
- Immutable capability preflight, direct argv encoding, exact semicolon handling,
  concurrent stream drains, and bounded cancellation cleanup.
- An internal result and lifecycle surface that reserves zero-valued enum members
  for unknown states and gives disposal only to resource-owning handles.

## Rejected risks

- Line/event capture loses byte identity, embedded newlines, terminators, and
  incomplete UTF-8 while reconstructing byte fields from text.
- Persistent control changes public attachment observations and relies on
  protocol correlation that can collide with user output.
- Persistent-control writes do not observe caller cancellation while a payload
  write is blocked, and its spike cleanup budget uses wall-clock time.
- Shell command construction, whole-process-tree termination, global
  throw-on-tmux-error policy, and conflating independent requests with semicolon
  groups are outside the accepted transport.
- The disposable public contender interface, no-op stateless disposal, and
  affirmative zero-valued enums are study scaffolding, not production API.

## Remaining unknowns

- macOS runtime behavior; the retained matrix ran on Linux.
- Production resource-ceiling values under representative workloads.
- End-to-end socket-name and full connection-option behavior in the production
  transport.
- Version behavior for command flags and format fields and operators; this
  transport matrix did not establish those ledger rows.
- The complete command-policy catalog beyond the currently encoded mappings.

## Critic dispositions

Framework-design, Python-parity, and tmux-protocol reviews are recorded in
`evidence/0001/critic-reviews.md`. Every accepted finding is either represented
as a production graft, used to reject a contender, or resolved by causal
lane-attributed evidence. No review blocks the raw-byte decision.

## Post-matrix evidence-tool review

After the three contender reviews were frozen at the evaluated matrix commit,
a final evidence audit found that removal validation required `HEAD` equality
and exact live solution membership. Evidence-tool commit
`007c3c3ea5df4761dd70142fd7b64bd3b647e336` replaces equality with evaluated
commit ancestry, retains live monotonic absence checks, and treats remaining
solution projects as a generation-time snapshot. This changes historical proof
validation, not the measured contender behavior or winner.

## Study-source removal proof

`evidence/0001/deletion.json` was generated from the staged index and current
solution. It proves that both transport project directories and their tracked
prefix are absent, the solution contains no transport-bakeoff project token,
and the shared support, support tests, and test child remain. The evaluated
source and mapped tests remain inspectable at the recorded Git revisions. The
validator requires that evaluated source commit in repository ancestry while
rechecking monotonic absence claims, so later solution changes do not rewrite
this historical snapshot.

`evidence/0001/SHA256SUMS` binds the complete retained evidence tree after this
proof was added.

## Machine-readable decision inputs

```json
{
  "schemaVersion": 1,
  "decisionId": "0001",
  "evaluatedCommit": "d1a018074cdfc5ca7408c75f7161245f4ae8363d",
  "decisionInputs": {
    "approvedDesign": "docs/superpowers/specs/2026-08-09-libtmux-csharp-design.md",
    "approvedPlan": "docs/superpowers/plans/2026-08-09-libtmux-csharp-bakeoffs.md",
    "pythonSourceRevision": "c4a980b32fedb10539fddf836373e4618c53731c",
    "sourceTreeFingerprint": "ee990952d56320e398b10c5b9490081472aac66bf7d4f5fa5b21a9a138efaeb1",
    "contenderRevisions": {
      "rawBytePump": [
        "b978c1523090ae164dcaf37631d6b7ce7eb44d75",
        "9851534da895baf9ec9b0387e45f3c9ecfd45e63"
      ],
      "lineEvent": [
        "66cdace10b20848fccee3a3175226ff2a89529f6",
        "7e7d73ad2a1c0f291002a293760f02145c6cf265",
        "498d89162e60c88c8a0006a04afd1473632595d8"
      ],
      "persistentControl": [
        "3289cf60590eaa096b56ac10d62062640651c144",
        "7e42a630858f51ed2df2e389c22dfec62dba8d72",
        "dc2d62e5d460517f716fd9df5c119a1beb19b4b4",
        "b2d0ebda95afcc88d86bc52d2e779a23ec4db58a"
      ]
    },
    "reviewFixRevisions": {
      "matrixBound": [
        "c7b150db81de5bc7a57004a1667a1f9cc6b5f39c",
        "d1a018074cdfc5ca7408c75f7161245f4ae8363d"
      ],
      "postMatrixEvidenceValidation": [
        "007c3c3ea5df4761dd70142fd7b64bd3b647e336"
      ]
    },
    "workloadContract": "csharp/spikes/LibTmux.TransportBakeoff/TransportContracts.cs",
    "corpus": "csharp/spikes/LibTmux.TransportBakeoff.Tests/TransportOracleCases.cs",
    "matrix": "evidence/0001/results.ndjson",
    "environment": "evidence/0001/environment.json",
    "pythonParity": "csharp/docs/parity/python-public-api.json",
    "errorPolicies": "csharp/docs/parity/error-policies.json",
    "versionLedger": "csharp/docs/parity/version-deltas.json"
  },
  "commands": [
    "cd csharp && eng/tmux/run-matrix.sh --include-master-advisory --evidence-dir artifacts/evidence-staging/0001/matrix spikes/LibTmux.TransportBakeoff.Tests/LibTmux.TransportBakeoff.Tests.csproj",
    "uv run python csharp/eng/evidence/assemble_bundle.py --producer matrix=csharp/artifacts/evidence-staging/0001/matrix --output csharp/docs/decisions/evidence/0001",
    "uv run python csharp/eng/parity/reconcile_versions.py --evidence csharp/docs/decisions/evidence/0001/results.ndjson --write",
    "uv run python csharp/eng/evidence/validate.py --phase pre-deletion csharp/docs/decisions/evidence/0001",
    "uv run python csharp/eng/evidence/record_deletion.py --solution csharp/LibTmux.slnx --absent csharp/spikes/LibTmux.TransportBakeoff --absent csharp/spikes/LibTmux.TransportBakeoff.Tests --tracked-prefix csharp/spikes/LibTmux.TransportBakeoff --project-token LibTmux.TransportBakeoff --output csharp/docs/decisions/evidence/0001/deletion.json",
    "uv run python csharp/eng/evidence/hash_tree.py csharp/docs/decisions/evidence/0001",
    "uv run python csharp/eng/evidence/validate.py csharp/docs/decisions/evidence/0001"
  ],
  "hardGates": [
    {
      "name": "required version and framework matrix",
      "status": "passed",
      "evidence": "evidence/0001/results.ndjson contains 14 passing required lanes with 139 tests per lane"
    },
    {
      "name": "raw data fidelity and framing",
      "status": "passed",
      "evidence": "The required matrix exercises raw bytes, embedded newlines, partial final records, every framing split, and invalid UTF-8"
    },
    {
      "name": "grouping and conservative attribution",
      "status": "passed",
      "evidence": "evidence/0001/protocol-transcripts/semicolon-middle-failure.txt and the required matrix distinguish independent requests from semicolon groups"
    },
    {
      "name": "bounded cancellation and client cleanup",
      "status": "passed",
      "evidence": "evidence/0001/protocol-transcripts/control-cancellation.txt and the required matrix prove dispatch attribution, settlement, and client reap"
    },
    {
      "name": "capability transparency",
      "status": "passed",
      "evidence": "Unsupported workload requirements and socket-name endpoints are rejected before dispatch without skipped matrix tests"
    },
    {
      "name": "default client transparency",
      "status": "passed",
      "evidence": "evidence/0001/protocol-transcripts/client-observability.txt proves the persistent client is observable and supports the one-shot default"
    },
    {
      "name": "public evidence redaction",
      "status": "passed",
      "evidence": "evidence/0001/redaction-proof.json and bundle validation reject local identity, paths, sockets, environment values, and tokens"
    },
    {
      "name": "adversarial review resolution",
      "status": "passed",
      "evidence": "evidence/0001/critic-reviews.md records three complete reviews with every accepted finding resolved"
    },
    {
      "name": "historical removal-proof durability",
      "status": "passed",
      "evidence": "evidence/0001/deletion.json is rechecked through ancestor-bound source provenance and monotonic absence claims without freezing later solution membership"
    }
  ],
  "winner": "one-shot raw-byte process transport",
  "grafts": [
    "Direct ArgumentList argv construction with literal trailing-semicolon encoding and structural semicolon groups",
    "Concurrent raw stdout and stderr capture with byte-length framing and exact invalid-byte projection",
    "Python-compatible stream-specific line views and command policies above authoritative raw results",
    "Complete socket-path and socket-name connection options with child-environment isolation",
    "Immutable capability preflight and explicit independent-request versus semicolon-group modeling",
    "Caller-token cancellation with bounded client-PID-only kill, stream settlement, and reap",
    "Configured resource ceilings for raw workloads, captured streams, and framed fields",
    "Internal lifecycle and outcome types with unknown as the default state"
  ],
  "rejectedRisks": [
    "Line-event fidelity loss and reconstructed raw-byte fields",
    "Persistent-control attachment observability and sentinel-based protocol ambiguity",
    "Persistent-control blocked-write cancellation gap and wall-clock cleanup accounting",
    "Shell command strings and whole-process-tree termination",
    "Global throw-on-tmux-error behavior and conflated grouping semantics",
    "Public disposable contender API with no-op disposal and affirmative default enum states"
  ],
  "remainingUnknowns": [
    "macOS runtime behavior because the retained matrix ran on Linux",
    "production resource-ceiling values under representative workloads",
    "end-to-end socket-name and complete connection-option behavior in production",
    "version behavior for command flags and format fields and operators",
    "the complete command-policy catalog beyond the currently encoded mappings",
    "control-block and diagnostic-event ceiling values for any future persistent control opt-in"
  ],
  "capabilities": [
    "one tmux client process per logical request",
    "authoritative raw stdout and stderr bytes",
    "byte-length field framing across arbitrary chunks",
    "deterministic invalid UTF-8 backslash projection",
    "direct argv with explicit independent and semicolon-group semantics",
    "nonzero tmux exits retained as raw observations",
    "pre-dispatch capability rejection without side effects",
    "typed post-start cancellation with bounded client-only cleanup"
  ],
  "evidenceFiles": [
    "evidence/0001/environment.json",
    "evidence/0001/results.ndjson",
    "evidence/0001/redaction-proof.json",
    "evidence/0001/protocol-transcripts/control.txt",
    "evidence/0001/protocol-transcripts/pty.txt",
    "evidence/0001/protocol-transcripts/control-contender.txt",
    "evidence/0001/protocol-transcripts/control-cancellation.txt",
    "evidence/0001/protocol-transcripts/semicolon-middle-failure.txt",
    "evidence/0001/protocol-transcripts/client-observability.txt",
    "evidence/0001/critic-reviews.md",
    "evidence/0001/deletion.json",
    "evidence/0001/SHA256SUMS"
  ],
  "criticDispositions": "evidence/0001/critic-reviews.md"
}
```
