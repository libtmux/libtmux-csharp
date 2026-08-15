# Transport bakeoff critic reviews

The final reviews are bound to the same clean commit as the matrix evidence.

```json
{
  "schemaVersion": 1,
  "evaluatedCommit": "d1a018074cdfc5ca7408c75f7161245f4ae8363d",
  "reviews": [
    {
      "critic": "framework-design-guidelines",
      "findings": [
        {
          "finding": "Version reconciliation could report success without consuming or persisting the supplied matrix evidence",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The version ledger binds four transport capabilities to the evaluated commit, seven release source commits, both frameworks, and xUnit tests present in that Git tree. The reconciler validates the exact grid and writes atomically, while inventory generation preserves valid evidence-owned fields. Matrix validation, reconciliation validation, inventory generation check, and ledger verification exit successfully."
        },
        {
          "finding": "Generic protocol traffic did not prove cancellation tombstone draining, failed-group attribution, or observable control-client lifecycle for each required lane",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "results.ndjson contains 14 passing required lanes and two passing advisory lanes with 139 tests each. client-observability.txt, control-cancellation.txt, and semicolon-middle-failure.txt contain one unique expected event set for every required framework and tmux release lane. The evidence validator derives the required lanes from results.ndjson and rejects missing, duplicate, malformed, or advisory semantic records."
        },
        {
          "finding": "Persistent-control disposal could consume its cleanup reserve while waiting for startup settlement",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "ControlModeTransport establishes one disposal deadline before settlement and carries it through cleanup. The delayed-start and startup-held lifecycle regressions exercise the bound in the 139-test corpus executed by every matrix lane."
        },
        {
          "finding": "The raw contender line projections are not exact Python result semantics",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "RawBytePumpTransport removes one terminal empty entry identically for both streams, while the pinned Python command result removes every trailing empty stdout entry, filters every empty stderr entry, and handles has-session separately. The decision keeps captured bytes authoritative and carries stream-specific Python line normalization and command policy as production grafts; contender projection code is not copied."
        },
        {
          "finding": "The transport matrix does not establish the complete connection-options contract",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "Every transport contender reports SocketName as unsupported, while the shared probe proves both socket-path and socket-name endpoint mechanics. The approved connection contract also includes configuration, color, initialization, socket-name factory, precedence, and child environment. The decision limits the measured claim and carries complete endpoint and connection-option handling as a production graft with real integration coverage."
        },
        {
          "finding": "Caller cancellation is not live while a persistent-control payload is blocked in the stdin write path",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "ControlModeTransport writes and flushes a dispatched payload with the connection lifetime cancellation token; caller cancellation is observed after that write completes. Persistent control is rejected as the first-release default, and live post-dispatch caller cancellation remains a requirement for any future control implementation."
        },
        {
          "finding": "Text contenders expose reconstructed bytes through fields that imply captured raw output",
          "severity": "low",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The line-event and persistent-control contenders encode decoded line collections into StandardOutput and StandardError byte fields. Both text contenders are rejected for the raw-result contract, and their reconstructed byte projections are excluded from production grafts."
        },
        {
          "finding": "The shared spike API has meaningless disposal and affirmative default enum states that are unsuitable as production lifecycle contracts",
          "severity": "low",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The disposable spike interface gives stateless contenders no-op disposal while affirmative execution and completion states use zero-valued enums. The production decision retains an internal stateless one-shot seam, reserves disposal for resource-owning handles, and does not copy the spike enum ordinals or public contender interface."
        },
        {
          "finding": "Persistent-control cleanup bounds rely on wall-clock time",
          "severity": "low",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "ControlModeTransport derives disposal and cleanup deadlines from wall-clock time. Persistent control is rejected for the first release; monotonic elapsed-time accounting or an injected TimeProvider is required for any future resource-owning control connection."
        }
      ]
    },
    {
      "critic": "python-parity",
      "findings": [
        {
          "finding": "Version capability claims require complete matrix evidence and mapped tests from the evaluated Git tree",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The reconciled version ledger binds attachment accounting, byte-length framing, control notifications, and semicolon grouping to the evaluated commit and results.ndjson. The reconciliation and matrix validators exit successfully."
        },
        {
          "finding": "Raw transport projections must reproduce Python stdout, stderr, and has-session behavior above the authoritative byte result",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The pinned Python command path removes every trailing stdout empty entry, filters every empty stderr entry, projects has-session stderr to stdout when needed, and uses UTF-8 backslash replacement. RawBytePumpTransport preserves the required bytes but applies different line normalization, so the winner includes the Python-compatible projection graft."
        },
        {
          "finding": "Generic nonzero tmux exits must remain results rather than transport exceptions",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The pinned Python command path returns exit code and streams; has-session maps the code to bool, list accessors are lenient, is-alive returns false, and raise-if-dead is explicit. The raw observation retains exit code and streams while the higher API layer owns command policy."
        },
        {
          "finding": "The line-event contender cannot satisfy raw-byte, embedded-newline, and invalid UTF-8 requirements",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "LineEventTransport declares raw-byte fidelity, embedded newlines, and exact UTF-8 replacement unsupported. RawBytePumpTransport declares and exercises all three, so line-event is rejected as the base transport."
        },
        {
          "finding": "Persistent control cannot be the default without changing Python-visible client and attachment semantics",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The pinned Python command path is one-shot while client and attached-session accessors expose live tmux state. client-observability.txt proves across every required lane that control is visible, changes attachment selection, and fires attach and detach hooks, so persistent control is rejected as the default."
        },
        {
          "finding": "The evaluated contenders do not cover Python socket-name connections",
          "severity": "low",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The pinned Python command path supports socket names and paths and the shared probe implements both forms, but each contender rejects SocketName before dispatch. The winner therefore requires full connection-option and child-environment handling as a production graft and records socket-name execution as outside this matrix."
        },
        {
          "finding": "Typed independent batches and semicolon groups conflict with Python command grouping",
          "severity": "low",
          "disposition": "rejected",
          "resolution": "not-applicable",
          "evidence": "The pinned Python API sends a structural semicolon in one direct argv call. The shared oracle correctly lets independent requests execute their suffix, lets tmux suppress a grouped suffix, and leaves failed one-shot member outcomes Unknown without per-member evidence."
        },
        {
          "finding": "Caller-token cancellation blocks Python parity for the raw winner",
          "severity": "low",
          "disposition": "rejected",
          "resolution": "not-applicable",
          "evidence": "The pinned Python command execution is synchronous and defines no async cancellation contract. Pre-dispatch no-side-effect cancellation and post-dispatch typed attribution with bounded client-PID-only reap are an intentional C# adaptation."
        }
      ]
    },
    {
      "critic": "tmux-protocol",
      "findings": [
        {
          "finding": "Only the one-shot raw-byte contender satisfies the required data-fidelity contract",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "RawBytePumpTransport supports embedded newlines, partial final lines, raw-byte fidelity, and exact invalid-byte projection. LineEventTransport supports only partial final lines, and ControlModeTransport supports none of those four requirements; both reject unsupported workloads before dispatch. Every required and advisory lane passes the 139-test capability corpus. The decision selects the raw-byte pump and rejects line-event and persistent control as defaults."
        },
        {
          "finding": "The transport decision requires causal lane-attributed semantic evidence",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The three semantic transcripts are written only after cancellation, side-effect, outcome, attachment, hook, and restoration assertions succeed. The durable files contain one exact event set for each of the 14 required framework and tmux-version lanes, exclude advisory master, and satisfy exact-multiset validation."
        },
        {
          "finding": "The contender matrix covers socket paths but not socket names",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "All three contenders return Unsupported for SocketName before process start, while the shared probe proves socket-name argument construction. The decision limits the measured conclusion to SocketPath and requires a production graft with real socket-path and socket-name transport tests."
        },
        {
          "finding": "Persistent control changes attachment-visible server state",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "client-observability.txt proves in every required lane that the control client is visible, changes attachment and unattached-session selection during its lifetime, fires one attached and one detached hook, and restores the prior state after disposal. The decision rejects persistent control as the default and reserves it for an explicit capability-bearing opt-in."
        },
        {
          "finding": "The spike transports do not impose production resource ceilings",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The raw contender has no configured capture limit, its byte-length decoder accepts declared fields up to an implementation integer limit, and control parsing retains lines and events without configured caps. The raw production graft caps workload members, captured stream bytes, and frame-field bytes while preserving bounded client kill and reap on limit failure. Any future control opt-in separately requires control-block and diagnostic-event ceilings."
        },
        {
          "finding": "A stable self-targeted refresh-client pause or continue line can enter its command guard",
          "severity": "low",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "Pinned tmux 3.2a through 3.7b sources synchronously write these self-targeted control notifications inside the issuing command guard. Pinned advisory master routes the lines through guarded notification deferral. The decision rejects persistent control as the default."
        },
        {
          "finding": "Control-mode reply sentinels share the user-output channel",
          "severity": "low",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "ControlModeTransport emits a random display-message body and recognizes a one-line equality match. Exact user-output collision remains possible, so the decision rejects persistent control and sentinel-based attribution from the default transport."
        }
      ]
    }
  ]
}
```
