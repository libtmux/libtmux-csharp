# ADR 0002: Immutable hierarchy object model

## Status

Accepted for the first production object model.

## Context

The bakeoff compared three public shapes against one shared topology, lifecycle,
and generation-safety corpus: mutable hierarchy entities, immutable services,
and an immutable hierarchy. The evaluated tree is
`6f6b0c6debe90447d42b4c1dd4b1efd571824f43` with source fingerprint
`40ee8b4ee16c285f4c5a8e572ea7a9ee2d42edca68448360897334009cb52318`.

The retained matrix ran on Linux with .NET SDK 10.0.302, `net8.0` and
`net10.0`, tmux 3.2a through 3.7b, and advisory tmux master. Model endpoint
execution covered socket paths. The advisory lanes and allocation observations
inform the decision but do not determine the winner.

Hard gates preceded allocation comparison. The hybrid is the only contender
that combines immutable receiver behavior, entity hierarchy methods, and
explicit relation availability.

| Requirement | Mutable hierarchy | Immutable services | Immutable hierarchy |
| --- | --- | --- | --- |
| Immutable receivers | No | Yes | Yes |
| Hierarchy methods | Yes | No | Yes |
| Explicit relation availability | No | Yes | Yes |
| NativeAOT execution | Yes | Yes | Yes |

## Decision

Use sealed immutable hierarchy entities with asynchronous entity methods,
immutable snapshot state, explicit captured relations, and separate owned
scopes. Refresh and Python-style mutate-and-return-self operations return
replacement objects. Borrowed `Server`, `Session`, `Window`, `Pane`, and
`Client` handles are not destructively disposable; only explicitly owned scopes
perform cleanup.

Bind entity identity to a validated positive `pid:start_time` generation using
tmux's `#{pid}:#{start_time}` formats. Session, window, and pane equality uses
generation plus typed ID. Client equality uses generation plus client name;
TTY is captured state. Server connection equality is defined separately.

Every targeted typed or raw mutation places generation validation and command
dispatch in the same tmux command list. A generation mismatch raises
`StaleServerGenerationException`; an ordinary missing target remains a
command-specific result or exception. Raw results expose the logical argv while
the generation guard remains internal.

Relations distinguish captured-empty from not-captured. Materialize one window
view per session relation path, compare linked views by generation-bound entity
key, and retain every session, index, and ordinal edge. Snapshot capture has an
explicit requested depth and does not claim transactionality across tmux
commands. Client attachment methods begin from fresh client state.

Compose the object model over ADR 0001's internal transport and typed lifecycle
behavior. Do not copy the public spike executors or contender vocabulary into
the production API.

## Matrix observations

Every required lane passed 224 tests with zero skips. Both advisory master lanes
also passed 224 tests with zero skips.

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

Named real-server tests establish linked and duplicate winlink edges, fresh
client attachment and control-client visibility, exact raw target injection,
hostile raw argv behavior, nonzero target results, and stale generation
rejection. The generation corpus also rejects malformed identities and proves
that a changed start time invalidates an entity even when the PID is unchanged.

## NativeAOT observations

All six contender and framework NativeAOT lanes published and executed. Each
binary contained only its selected contender graph. The API probe exercised
linked capture and replacement refresh behavior. Allocation values are
observations from this host, not acceptance thresholds or general performance
claims.

| Contender | `net10.0` allocated bytes | `net8.0` allocated bytes |
| --- | ---: | ---: |
| Mutable | 2,202,592 | 2,339,536 |
| Services | 295,080 | 411,520 |
| Hybrid | 286,048 | 283,944 |

## Production grafts

The production model must add the following behavior without copying a
contender wholesale:

- ADR 0001 one-shot transport, cancellation, cleanup, raw-result, and
  command-specific exception policies.
- Socket-path and socket-name connection options with explicit platform
  annotations and end-to-end tests.
- Generation-bound typed entity equality, separate server connection equality,
  and client identity that excludes TTY.
- Session-scoped window views with entity-key equality and complete duplicate
  relation-edge preservation.
- One edge-aware `list-panes -a` hierarchy scan with explicit requested capture
  depth and nontransactional snapshot semantics.
- Fresh client attachment resolution for session, window, pane, detached, and
  missing states.
- Python-compatible lenient list policies with explicit `IsAliveAsync` and
  `RaiseIfDeadAsync` operations.
- Atomic generation guards for every targeted typed and raw session, window,
  and pane mutation.
- Immutable request records, immutable relation storage, and
  replacement-returning entity operations.
- Separate idempotent owned scopes with bounded cleanup and ordinary
  non-disposable entity handles.
- Shipping-project trimming analysis, Public API validation, package
  validation, and platform annotations.
- Internal transport and materialization seams without public contender or
  executor vocabulary.

Sessions, clients, and attached sessions return empty snapshots for any
underlying list-command failure. Server-wide windows and panes suppress only
missing-daemon or missing-socket failures. Child traversal and native search
remain loud. Linked-session discovery returns empty if either required listing
fails. Cancellation and programmer errors are never suppressed.

## Rejected risks

- Mutable in-place entity refresh and shared-state aliasing as the public model.
- A service-only public surface that removes hierarchy methods.
- Destructive disposal on borrowed entity handles.
- TTY-based client identity and missing server connection equality.
- Global window interning or `Distinct`-based collapse of relation paths.
- PID-only generation identity, unsupported generation fields, and
  preflight-only stale checks.
- Unguarded raw entity commands or broad target retargeting.
- Two-read complete-hierarchy materialization presented as an atomic snapshot.
- One global throw-on-tmux-error policy or literal recreation of every Python
  query spelling.
- Public spike executors, contender names, and bakeoff-specific exception
  types.
- Allocation measurements or AOT spike success as sufficient production
  readiness evidence.
- Socket-name behavior represented as measured by this model matrix.

## Remaining unknowns

- macOS behavior because the retained model and NativeAOT evaluations ran on
  Linux.
- End-to-end socket-name behavior through the production connection and
  hierarchy surface.
- Representative production allocation and capture-depth behavior on large
  linked topologies.
- Shipping analyzer, Public API, package-validation, and platform-annotation
  results.
- The complete command-specific list and warning policy catalog.
- The complete command-flag and format-field or operator version surface.
- Same-generation topology churn during a multi-command capture because
  captures are not transactions.

Decision 0002 adds no command-flag or format-field or operator version
evidence. `version-deltas.json` therefore retains its prior evidence state.

## Critic dispositions

Framework-design, Python-parity, and tmux-protocol reviews are recorded in
`evidence/0002/critic-reviews.md`. Every accepted finding is represented by a
causal fix, a bounded measured claim, or a required production graft. No review
blocks the immutable hierarchy decision.

## Study-source removal proof

`evidence/0002/deletion.json` is generated from the staged index and current
solution. It proves the model runner and all six model project prefixes are
absent, the solution contains no model-bakeoff project token, and the shared
support, support tests, and test child remain. The validator retains the
evaluated source through Git ancestry while rechecking the live monotonic
absence claims.

`evidence/0002/SHA256SUMS` binds the complete retained evidence tree after the
proof is added.

## Machine-readable decision inputs

```json
{
  "schemaVersion": 1,
  "decisionId": "0002",
  "evaluatedCommit": "6f6b0c6debe90447d42b4c1dd4b1efd571824f43",
  "decisionInputs": {
    "approvedDesign": "docs/superpowers/specs/2026-08-09-libtmux-csharp-design.md",
    "approvedPlan": "docs/superpowers/plans/2026-08-09-libtmux-csharp-bakeoffs.md",
    "pythonSourceRevision": "c4a980b32fedb10539fddf836373e4618c53731c",
    "sourceTreeFingerprint": "40ee8b4ee16c285f4c5a8e572ea7a9ee2d42edca68448360897334009cb52318",
    "contenderRevisions": {
      "mutableHierarchy": [
        "a5a3bff9898d12fc5d2203dd1a4aaa657847d53c"
      ],
      "immutableServices": [
        "015b466b77a417caeef4793394fc3365f23555f4",
        "5bedad62c5d50e5b245ba2844b158f92ed6e11b1"
      ],
      "immutableHierarchy": [
        "817285a1e99abc5048e64362a6f87afa9b551676"
      ]
    },
    "reviewFixRevisions": [
      "6f6b0c6debe90447d42b4c1dd4b1efd571824f43"
    ],
    "evidenceToolRevision": "808d89c04e518b8a1ecbb4b95a82e7f7d34f9ca1",
    "workloadContract": "csharp/spikes/LibTmux.ModelBakeoff.Contracts/IModelScenarioRunner.cs",
    "corpus": "csharp/spikes/LibTmux.ModelBakeoff.Tests/ModelAcceptanceTests.cs",
    "matrix": "evidence/0002/results.ndjson",
    "aotResults": "evidence/0002/aot-results.ndjson",
    "allocations": "evidence/0002/allocations.ndjson",
    "apiExamples": "evidence/0002/api-examples.md",
    "environment": "evidence/0002/environment.json",
    "pythonParity": "csharp/docs/parity/python-public-api.json",
    "errorPolicies": "csharp/docs/parity/error-policies.json",
    "versionLedger": "csharp/docs/parity/version-deltas.json"
  },
  "commands": [
    "cd csharp && eng/tmux/run-matrix.sh --include-master-advisory --evidence-dir artifacts/evidence-staging/0002/matrix spikes/LibTmux.ModelBakeoff.Tests/LibTmux.ModelBakeoff.Tests.csproj",
    "cd csharp && eng/aot/run-model-aot.sh --evidence-dir artifacts/evidence-staging/0002/aot",
    "uv run python csharp/eng/evidence/assemble_bundle.py --producer matrix=csharp/artifacts/evidence-staging/0002/matrix --producer model-aot=csharp/artifacts/evidence-staging/0002/aot --output csharp/docs/decisions/evidence/0002",
    "uv run python csharp/eng/parity/reconcile_versions.py --evidence csharp/docs/decisions/evidence/0002/results.ndjson --write",
    "uv run python csharp/eng/evidence/validate.py --phase pre-deletion csharp/docs/decisions/evidence/0002",
    "uv run python csharp/eng/evidence/record_deletion.py --solution csharp/LibTmux.slnx --absent csharp/eng/aot/run-model-aot.sh --absent-glob csharp/spikes/LibTmux.ModelBakeoff* --tracked-prefix csharp/eng/aot/run-model-aot.sh --tracked-prefix csharp/spikes/LibTmux.ModelBakeoff.AotSmoke --tracked-prefix csharp/spikes/LibTmux.ModelBakeoff.Contracts --tracked-prefix csharp/spikes/LibTmux.ModelBakeoff.HybridHierarchy --tracked-prefix csharp/spikes/LibTmux.ModelBakeoff.ImmutableServices --tracked-prefix csharp/spikes/LibTmux.ModelBakeoff.MutableDirect --tracked-prefix csharp/spikes/LibTmux.ModelBakeoff.Tests --project-token LibTmux.ModelBakeoff --project-count 3 --output csharp/docs/decisions/evidence/0002/deletion.json",
    "uv run python csharp/eng/evidence/hash_tree.py csharp/docs/decisions/evidence/0002",
    "uv run python csharp/eng/evidence/validate.py csharp/docs/decisions/evidence/0002"
  ],
  "hardGates": [
    {
      "name": "required version and framework matrix",
      "status": "passed",
      "evidence": "evidence/0002/results.ndjson contains 14 passing required lanes with 224 tests and zero skips per lane"
    },
    {
      "name": "immutable hierarchy capability conjunction",
      "status": "passed",
      "evidence": "The shared corpus makes immutable hierarchy the only contender with immutable receivers, hierarchy methods, and explicit relation availability"
    },
    {
      "name": "generation-bound target safety",
      "status": "passed",
      "evidence": "The evaluated corpus validates positive pid:start_time identities and atomically guards typed and raw session targets, including same-PID generation change and reused-ID restart cases"
    },
    {
      "name": "relation and client observation fidelity",
      "status": "passed",
      "evidence": "The required matrix exercises linked and duplicate edges, capture availability, fresh client selection and detach state, and control-client visibility"
    },
    {
      "name": "NativeAOT static contender execution",
      "status": "passed",
      "evidence": "evidence/0002/aot-results.ndjson contains six passing contender and framework lanes with one selected contender graph per native binary"
    },
    {
      "name": "public evidence redaction",
      "status": "passed",
      "evidence": "evidence/0002/redaction-proof.json and evidence/0002/model-aot-redaction-proof.json cover the canonical sensitive-data categories"
    },
    {
      "name": "adversarial review resolution",
      "status": "passed",
      "evidence": "evidence/0002/critic-reviews.md records three complete reviews with every accepted finding resolved"
    },
    {
      "name": "historical study-source removal proof",
      "status": "passed",
      "evidence": "evidence/0002/deletion.json binds the staged model-source removal while preserving evaluated source ancestry and live absence checks"
    }
  ],
  "winner": "sealed immutable hierarchy with explicit relations and separate owned scopes",
  "grafts": [
    "ADR 0001 one-shot transport, cancellation, cleanup, raw-result, and command-specific exception policies",
    "Socket-path and socket-name connection options with explicit platform annotations and end-to-end tests",
    "Generation-bound typed entity equality, separate Server connection equality, and Client identity excluding TTY",
    "Session-scoped Window views with entity-key equality and complete duplicate relation-edge preservation",
    "One edge-aware list-panes hierarchy scan with explicit requested capture depth and nontransactional snapshot semantics",
    "Fresh Client attachment resolution for session, window, pane, detached, and missing states",
    "Python-compatible lenient list policies with explicit IsAliveAsync and RaiseIfDeadAsync operations",
    "Atomic generation guards for every targeted typed and raw Session, Window, and Pane mutation",
    "Immutable request records, immutable relation storage, and replacement-returning entity operations",
    "Separate idempotent owned scopes with bounded cleanup and ordinary non-disposable entity handles",
    "Shipping-project trimming analysis, Public API validation, package validation, and platform annotations",
    "Internal transport and materialization seams without public contender or executor vocabulary"
  ],
  "rejectedRisks": [
    "Mutable in-place entity refresh and shared-state aliasing as the public model",
    "A service-only public surface that removes hierarchy methods",
    "Destructive disposal on borrowed entity handles",
    "TTY-based Client identity and missing Server connection equality",
    "Global Window interning or Distinct-based collapse of session relation paths",
    "PID-only generation identity, unsupported generation fields, and preflight-only stale checks",
    "Unguarded raw entity commands or broad target retargeting",
    "Two-read complete-hierarchy materialization presented as an atomic snapshot",
    "One global throw-on-tmux-error policy or literal recreation of every QueryList spelling",
    "Public spike executors, contender names, and bakeoff-specific exception types",
    "Allocation measurements or AOT spike success as sufficient production-readiness evidence",
    "Socket-name behavior represented as measured by this model matrix"
  ],
  "remainingUnknowns": [
    "macOS behavior because the retained model and NativeAOT evaluations ran on Linux",
    "end-to-end socket-name behavior through the production connection and hierarchy surface",
    "representative production allocation and capture-depth behavior on large linked topologies",
    "shipping analyzer, Public API, package-validation, and platform-annotation results",
    "the complete command-specific list and warning policy catalog",
    "the complete command-flag and format-field or operator version surface",
    "same-generation topology churn during a multi-command capture because captures are not transactions"
  ],
  "capabilities": [
    "sealed immutable hierarchy entities with asynchronous methods",
    "generation-bound typed identity and replacement refresh",
    "explicit captured-empty and not-captured relation states",
    "session-scoped linked views with complete relation-edge preservation",
    "fresh client attachment observations and explicit control-client visibility",
    "atomic stale-generation guards for typed and raw target operations",
    "separate idempotent owned scopes for destructive cleanup",
    "static NativeAOT contender selection on net8.0 and net10.0"
  ],
  "evidenceFiles": [
    "evidence/0002/environment.json",
    "evidence/0002/results.ndjson",
    "evidence/0002/redaction-proof.json",
    "evidence/0002/protocol-transcripts/control.txt",
    "evidence/0002/protocol-transcripts/pty.txt",
    "evidence/0002/aot-results.ndjson",
    "evidence/0002/allocations.ndjson",
    "evidence/0002/api-examples.md",
    "evidence/0002/model-aot-redaction-proof.json",
    "evidence/0002/critic-reviews.md",
    "evidence/0002/deletion.json",
    "evidence/0002/SHA256SUMS"
  ],
  "criticDispositions": "evidence/0002/critic-reviews.md"
}
```
