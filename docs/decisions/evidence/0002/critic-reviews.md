# Object-model bakeoff critic reviews

The final reviews are bound to the same clean commit as the matrix and NativeAOT evidence.

```json
{
  "schemaVersion": 1,
  "evaluatedCommit": "6f6b0c6debe90447d42b4c1dd4b1efd571824f43",
  "reviews": [
    {
      "critic": "framework-design-guidelines",
      "findings": [
        {
          "finding": "A green neutral corpus alone does not select the production object model",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "Measured at 6f6b0c6debe90447d42b4c1dd4b1efd571824f43: all 16 matrix lanes passed 224 tests with zero skips, and all six Native AOT lanes passed. ModelOracleCases and HybridHierarchyScenarioRunner show that Hybrid alone combines immutable replacements, hierarchy methods, and explicit captured-versus-not-captured relations. The decision therefore selects Hybrid on those hard gates; the allocation rows, which favor Hybrid in both frameworks, are only a tie-breaker."
        },
        {
          "finding": "Borrowed entity handles and owned resources require separate lifecycle surfaces",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "Measured at 6f6b0c6debe90447d42b4c1dd4b1efd571824f43: Server, Session, Window, Pane, and Client are sealed non-disposable handles, while OwnedSessionScope alone implements IAsyncDisposable and performs generation-bound idempotent cleanup. The production graft preserves non-destructive borrowed handles and introduces disposable scopes only for resources explicitly created and owned by the library."
        },
        {
          "finding": "Entity identity is incomplete for Server and over-specified for Client",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "Measured at 6f6b0c6debe90447d42b4c1dd4b1efd571824f43: Session, Window, and Pane equality use generation plus typed tmux ID; Server exposes a generation but has no value equality; Client equality uses generation, client name, and TTY. The production graft adds explicit Server identity for its connection namespace and generation, retains generation-plus-ID identity for Session, Window, and Pane, and defines Client identity as generation plus client name while keeping TTY as snapshot data."
        },
        {
          "finding": "Global window interning and deduplicated session windows collapse parent-scoped linked paths",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "Measured at 6f6b0c6debe90447d42b4c1dd4b1efd571824f43: CaptureHierarchyAsync interns Window instances in one global dictionary and applies Distinct to Session.Windows, while Winlinks retain session, index, and edge ordinal. The neutral corpus proves one window entity key with two distinct edges, including duplicate links in one session; it does not require global object aliasing. The production graft exposes session-scoped window views, compares entities by key, and preserves every winlink edge without deduplicating relationship paths."
        },
        {
          "finding": "Client navigation must resolve current attachments rather than expose only captured IDs",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "Measured at 6f6b0c6debe90447d42b4c1dd4b1efd571824f43: Client.RefreshAsync returns a replacement with current typed attachment IDs, and the shared PTY scenario proves selection changes and detach state are observed. The scenario then resolves Session, Window, and Pane manually through the Server hierarchy. The production graft adds fresh Client attachment-resolution methods that re-read the client and return typed hierarchy handles or detached results."
        },
        {
          "finding": "The hybrid spike duplicates transport and exception policy instead of composing the accepted transport decision",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "Measured at 6f6b0c6debe90447d42b4c1dd4b1efd571824f43: EndpointHybridExecutor owns ProcessStartInfo construction, stream pumping, cancellation cleanup, and spike-specific InvalidOperationException, AggregateException, and HybridTargetMissingException classification. ADR 0001 already selects the internal one-shot raw-byte transport and assigns command policy to higher layers. The production graft carries only the immutable hierarchy model over that transport, including its direct argv, typed command and cancellation failures, bounded client-only cleanup, and raw-result policy."
        },
        {
          "finding": "List-shaped accessors throw where the public failure policy requires lenient defaults",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "Measured at 6f6b0c6debe90447d42b4c1dd4b1efd571824f43: Hybrid GetSessionsAsync, GetWindowsAsync, GetPanesAsync, and CaptureClientsAsync convert tmux list failures into InvalidOperationException. The frozen command-policy ledger requires empty results for the preserved lenient list accessors and separate liveness or explicit raise APIs when callers need failure distinction. The production graft applies that policy above ADR 0001 raw results rather than copying the spike's global throw behavior."
        },
        {
          "finding": "Native AOT smoke results do not establish shipping analyzer, API-baseline, or platform-support guarantees",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "Measured at 6f6b0c6debe90447d42b4c1dd4b1efd571824f43: six linux-x64 Native AOT publish-and-run lanes pass for the three contenders across net8.0 and net10.0, exercising linked materialization and refresh. Recommended .NET analyzer diagnostics are treated as build failures, but the PublicApiAnalyzers version has no model-project reference, the smoke project suppresses documentation warnings, and no supported-platform annotations are present. The decision limits the evidence claim to AOT viability and requires shipping analyzer gates, an explicit public API baseline, platform annotations, and supported-platform AOT coverage in production."
        },
        {
          "finding": "Study-only contender and executor vocabulary is exposed as public API",
          "severity": "low",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "Measured at 6f6b0c6debe90447d42b4c1dd4b1efd571824f43: ModelBakeoff namespaces publicly expose contender runners, IHybridExecutor, EndpointHybridExecutor, IHybridClientProcess, model observations, and spike exception names. The resolution removes the study projects after retaining evidence and grafts only domain-facing immutable Server, Session, Window, Pane, Client, relation, and ownership concepts; transport and evaluation seams remain internal."
        }
      ]
    },
    {
      "critic": "python-parity",
      "findings": [
        {
          "finding": "The winner must combine immutable replacement handles, hierarchy navigation, and explicit relation availability rather than win on allocation or AOT viability alone",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "Measured at 6f6b0c6debe90447d42b4c1dd4b1efd571824f43: only hybrid-hierarchy combines non-mutating replacement handles, hierarchy methods, and captured-versus-not-captured relations. All 16 matrix lanes pass 224 tests with zero skips, and all six model-AOT lanes pass. The decision selects that conjunction; allocation data is only a tie-breaker and AOT establishes viability rather than Python parity."
        },
        {
          "finding": "Global Window deduplication loses Python's session-scoped views and duplicate winlink paths",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The pinned Python revision c4a980b32fedb10539fddf836373e4618c53731c queries Session.windows through list-windows targeted to that session. At 6f6b0c6debe90447d42b4c1dd4b1efd571824f43, the shared corpus proves one generation-bound Window entity may have two cross-session or same-session edges, while hybrid equality uses generation plus WindowId and Winlink retains session, index, and ordinal. The production graft builds each session-facing Window view from its winlinks without path deduplication, preserves every edge, and compares entity handles by generation-bound identity."
        },
        {
          "finding": "Client identity and live attachment must not be derived from stale attachment fields or an over-broad key",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The pinned Python Client documentation identifies client_name as stable identity and requires attached_session, attached_window, and attached_pane to re-read tmux on every access. At 6f6b0c6debe90447d42b4c1dd4b1efd571824f43, the matrix proves fresh switch and detach observations plus control-client visibility, but the contender key also carries client_tty. The production graft keys Client by server generation plus client_name, keeps tty and attachment fields as snapshot data, and provides fresh attachment methods that return null when the client or attachment is absent."
        },
        {
          "finding": "Session-only raw execution does not cover the full hierarchy command, generation, and list-failure contracts",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At 6f6b0c6debe90447d42b4c1dd4b1efd571824f43, the corrected corpus proves direct-argv raw Session target overrides, hostile argument fidelity, same-generation target failures as raw nonzero results, positive pid:start_time validation, same-PID start-time changes, and atomic stale-generation rejection before mutation. The production graft applies equivalent raw overrides to Session, Window, and Pane, guards every targeted mutator atomically, uses the ADR 0001 transport and typed command policies across Server, Session, Window, and Pane, and preserves Python's empty-by-default Server.sessions, Server.clients, and Server.attached_sessions policy with explicit liveness and raise-if-dead APIs."
        },
        {
          "finding": "Captured graphs require explicit depth, eager materialization, relation availability, and a documented nontransactional boundary",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At 6f6b0c6debe90447d42b4c1dd4b1efd571824f43, CapturedRelation distinguishes captured-empty from not-captured, captured enumeration performs no tmux calls, and hierarchy rows are generation-bound. CaptureHierarchyAsync reads windows and panes in separate tmux commands, so graph capture is not a tmux transaction. The production graft exposes requested capture depth, eagerly materializes that depth, retains explicit availability at each relation, and documents that concurrent topology changes may span the component reads."
        },
        {
          "finding": "Python's mutable refresh, self-return patterns, destructive context managers, and snapshot-wide dataclass equality should be copied literally into C#",
          "severity": "low",
          "disposition": "rejected",
          "resolution": "not-applicable",
          "evidence": "The pinned Python revision uses mutable dataclasses and destructive Session and Pane context-manager cleanup, while the 6f6b0c6debe90447d42b4c1dd4b1efd571824f43 hybrid corpus proves replacement refreshes, generation-and-typed-ID equality, non-disposable borrowed handles, explicit Kill operations, and a separate OwnedSessionScope for owned cleanup. This intentionally preserves observable tmux behavior without importing Python object-lifetime and equality mechanics into the C# API."
        },
        {
          "finding": "Python QueryList's complete double-underscore operator vocabulary must become a one-for-one C# public collection API",
          "severity": "low",
          "disposition": "rejected",
          "resolution": "not-applicable",
          "evidence": "The parity inventory binds 626 Python symbols to c4a980b32fedb10539fddf836373e4618c53731c, including the internal experimental QueryList and its string-selected comparison operators. At 6f6b0c6debe90447d42b4c1dd4b1efd571824f43, contenders expose owned read-only collections, explicit relation availability, typed IDs, and ordinary typed LINQ selection. The production collection vocabulary remains narrow, adding named typed helpers only for demonstrated callers while the ledger tracks behavioral coverage separately from public API spelling."
        }
      ]
    },
    {
      "critic": "tmux-protocol",
      "findings": [
        {
          "finding": "Generation identity must use tmux's supported start_time format and validate both tuple members",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At commit 6f6b0c6debe90447d42b4c1dd4b1efd571824f43, every contender and shared oracle uses #{pid}:#{start_time}; server_start_time is absent. All three contenders require exactly one positive numeric PID and one positive numeric start time. Same_pid_with_a_new_start_time_rejects_the_stale_entity deterministically changes 4242:100 to 4242:101, while Initial_generation_rejects_a_malformed_tuple rejects a malformed first response. Pinned tmux 3.2a, 3.7b, and master format tables expose pid and start_time. All 16 matrix rows record 224 passing tests, and the matrix runner classifies a lane as passed only with zero skips."
        },
        {
          "finding": "Raw session commands require the generation guard and target command in one tmux command group without reparsing logical argv",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At commit 6f6b0c6debe90447d42b4c1dd4b1efd571824f43, Mutable, Services, and Hybrid each dispatch one top-level argv sequence shaped as if-shell -F condition empty-true-command unique-failure-token ; logical-command -t target remainder. A false condition parses the unique token as an invalid command; pinned tmux 3.2a and 3.7b cmd-if-shell and command-queue sources report failure and remove later members of that command group. Classification requires the exact whole stderr line for the per-call token, while same-generation target failures remain raw results. The 224-test matrix corpus includes restart rejection, explicit target override, exact logical argv projection, same-generation missing-target classification, and real-server names containing apostrophes, semicolons, format tokens, quotes, brackets, and backslashes."
        },
        {
          "finding": "Complete Hybrid hierarchy capture joins two independently mutable topology scans",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At commit 6f6b0c6debe90447d42b4c1dd4b1efd571824f43, Hybrid CaptureHierarchyAsync still executes list-windows -a followed by list-panes -a, so a same-generation topology change can mix two capture times. The production graft uses one edge-aware list-panes -a -F scan and does not describe multi-command capture as atomic. Pinned tmux 3.2a and 3.7b sources show that list-panes -a visits every session winlink and supplies session, winlink, window, and pane format context. Materialization deduplicates each window and its pane entities while retaining every winlink path and every path duplicate in server-wide window and pane enumeration. The matrix corpus verifies cross-session links, same-session duplicate winlinks, parent-session lookup scoping, and linked-pane identity."
        },
        {
          "finding": "The spike list APIs do not implement the approved command-specific leniency policy",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At commit 6f6b0c6debe90447d42b4c1dd4b1efd571824f43, contender session and client list paths generally convert nonzero tmux results to exceptions, and the model corpus has no fault-class oracle for the approved list policy. The production graft returns empty snapshots for session, client, and attached-session list-command failures; suppresses only missing-daemon or missing-socket failures for server-wide windows and panes; keeps session and window child traversal and native search loud; returns empty linked sessions when either source listing fails; and never converts cancellation or programmer failures to empty results."
        },
        {
          "finding": "Spike client keys include TTY even though approved client identity is generation plus client name",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At commit 6f6b0c6debe90447d42b4c1dd4b1efd571824f43, ClientEntityKey and both immutable contender ClientKey types contain generation, client name, and TTY, and Hybrid Client equality compares the whole key. The approved production graft defines equality and hashing from generation plus client_name only; client_tty remains captured state and a targeting aid. Fresh attachment resolution and control-client visibility remain supported independently of that identity correction."
        },
        {
          "finding": "Model endpoint evidence covers socket paths but not named sockets",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At commit 6f6b0c6debe90447d42b4c1dd4b1efd571824f43, every real-server model and AOT start uses SocketPath. The concrete Services and Hybrid executors reject SocketName, while Mutable delegates endpoint mechanics to the shared probe. The decision therefore bounds model conclusions to SocketPath and carries the transport decision's production endpoint graft for both -S paths and -L names, including precedence and child-environment behavior. All 16 matrix rows and all six AOT rows are bound to this commit."
        },
        {
          "finding": "Generic control and PTY transcripts are not lane-attributed or feature-specific evidence for generation, raw guards, ownership, and hierarchy",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At commit 6f6b0c6debe90447d42b4c1dd4b1efd571824f43, producer metadata binds both evidence producers to one clean source fingerprint; results.ndjson contains 14 required and two advisory passing rows with 224 tests each, and aot-results.ndjson contains six passing contender-framework rows. The unfiltered matrix command and its zero-skip pass rule causally bind named source tests to every row. The repeated control and PTY event files contain no framework or tmux-version labels, so the decision uses them only as harness corroboration. Generation, guard, ownership, client, and linked-topology claims are instead bound to their named real-server tests and the pinned 3.2a and 3.7b protocol sources; AOT claims are limited to the linked-capture and rename probe executed by the six native binaries."
        },
        {
          "finding": "Hybrid owned-session compensation and client visibility use tmux-supported semantics across the measured socket-path matrix",
          "severity": "low",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At commit 6f6b0c6debe90447d42b4c1dd4b1efd571824f43, owned creation combines the generation guard with new-session -e, a random session-environment marker, exact generation-and-marker recovery through list-sessions -f, guarded kill, bounded non-caller cleanup, collision safety, and preservation of both operation and cleanup failures. Client capture uses client_control_mode and resolves attachment state afresh. Pinned tmux 3.2a, 3.7b, and master sources support if-shell -F, new-session -e, list-sessions -f, session environment expansion, and client_control_mode. The 224-test matrix corpus exercises cancellation at multiple handoff phases, malformed projection recovery, successor isolation, existing-name collision safety, cleanup failure propagation, PTY switch and detach, and visible control clients. The decision accepts these semantics for SocketPath and keeps ordinary entity handles non-owning."
        }
      ]
    }
  ]
}
```
