# Query bakeoff critic reviews

The final reviews are bound to the same clean commit as the matrix and NativeAOT evidence.

```json
{
  "schemaVersion": 1,
  "evaluatedCommit": "953a1970d91bbe319906a8a2e294799eb4b966ca",
  "reviews": [
    {
      "critic": "framework-design-guidelines",
      "findings": [
        {
          "finding": "Passing semantic and NativeAOT lanes does not by itself select the production catalog",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At commit 953a1970d91bbe319906a8a2e294799eb4b966ca all 14 required matrix lanes pass 650 tests and all six catalog and framework NativeAOT lanes pass, while the allocation leader differs by framework. Generated alone reports deterministic LTQG001 through LTQG008 build errors for duplicate or invalid fields, owner and value mismatches, relation mismatches, and closed-schema omissions, and emits no partial catalog after an error. The decision selects Generated on that compile-time integrity gate and treats allocation rows only as host observations."
        },
        {
          "finding": "Catalog and planner capability seams are unsafe public production boundaries",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "IFieldCatalogContender, all three contender classes, QueryPhysicalFieldCapability, QueryPlannerCapabilities, QueryListCommandCapability, and raw command construction are public. The remaining public QueryPlannerCapabilities constructor accepts caller-supplied physical mappings, and planning verifies target and materializability but does not prove that a wire field maps to the frozen tmux format. A hostile session_id to session_name mapping can therefore turn an accepted Required plan into a silent false-negative filter. The production graft keeps the generated catalog and every physical or version profile internal and exposes only catalog-free query entry points, a read-only explain result, and the explicit unsafe-filter opt-in."
        },
        {
          "finding": "Logical query records do not provide structural value equality",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "AndNode and OrNode are positional records over ImmutableArray<QueryNode>; their generated equality and hash behavior follows ImmutableArray instance equality rather than element-wise graph equality. The translation tests use a bespoke recursive AssertNodeEqual helper, which avoids exercising the public record equality contract. The production graft either implements structural equality and hashing for the public query graph or uses non-record reference types so the API does not promise misleading value semantics."
        },
        {
          "finding": "The generated catalog removes discovery but not runtime metadata dependence",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "GeneratedFieldCatalog declares UsesRuntimeReflection and RequiresPublicPropertyMetadata as true, its emitted lookup consumes MemberInfo and compares DeclaringType and Name, and captured-constant translation calls FieldInfo.GetValue. The decision describes the winner as eliminating runtime catalog discovery, not as reflection-free, and requires trimming annotations plus NativeAOT coverage for every supported captured-constant and member-resolution shape."
        },
        {
          "finding": "The NativeAOT smoke and allocation bundle does not establish shipping analyzer, package, or performance guarantees",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The six AOT rows are linux-x64 executions under SDK 10.0.302 of one nested-relation expression, canonical JSON, and the direct interpreter. The query libraries suppress documentation diagnostics, do not reference PublicApiAnalyzers, and have no package-validation or external consumer lane. The generator is compiled against Microsoft.CodeAnalysis.CSharp 5.6.0 but is not tested with the minimum SDK that can consume the net8.0 target. Production requires public-API baselines, package validation, trim and AOT analyzers, analyzer-asset isolation, supported-SDK consumer builds, and supported-platform NativeAOT tests; allocation values remain non-normative."
        },
        {
          "finding": "Version capability construction must use observed tmux versions rather than a source-label sentinel",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "QueryPlannerCapabilities accepts string versions and lists master as supported, while the retained matrix records both advisory master rows as failed because the built binary reports next-3.8. The measured support claim is limited to the 14 passing Linux lanes for tmux 3.2a through 3.7b on net8.0 and net10.0. Production consumes the internal parsed-version capability service, removes caller-authored version strings, and makes no current-master compatibility claim from this bundle."
        }
      ]
    },
    {
      "critic": "python-parity",
      "findings": [
        {
          "finding": "Delimiter-joined candidate rows do not preserve Python-visible field values",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At commit 953a1970d91bbe319906a8a2e294799eb4b966ca, QueryPlannerCapabilities.CreateListCommands joins Session, Window, Pane, and Client fields with a pipe, and PushdownDifferentialTests parses the decoded lines with String.Split. Separator bytes in projected names are therefore indistinguishable from row structure before residual evaluation. ADR 0001 makes raw bytes authoritative and requires byte-length framing. The production graft uses the ADR 0001 framed materializer and adds hostile delimiter, newline, and Unicode projection cases."
        },
        {
          "finding": "Target-only query command capabilities erase owner-scoped Python search semantics",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At commit 953a1970d91bbe319906a8a2e294799eb4b966ca, QueryPlannerCapabilities stores one list command per QueryTarget, and Window and Pane always use server-wide -a commands. The parity inventory includes Server search methods, Session-scoped window and pane searches, and Window-scoped pane search; ADR 0002 also assigns different failure policies to server-wide access, child traversal, and native search. The production graft selects commands from the owning Server, Session, or Window context and preserves parent targets, relation edges, and command-specific failure policy."
        },
        {
          "finding": "Residual relation plans do not declare the snapshot capture they require",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At commit 953a1970d91bbe319906a8a2e294799eb4b966ca, QueryPlan carries pushdown, residual, filter, and unsafe fields but no relation-depth requirement. Relation predicates remain residual, the real-tmux readers produce uncaptured relations, and the nested NativeAOT example hand-constructs captured relations. ADR 0002 requires explicit requested capture depth and distinct captured-empty and not-captured states. The production graft derives depth from the residual AST and captures it before evaluation or fails before enumeration with an incomplete-snapshot error."
        }
      ]
    },
    {
      "critic": "tmux-protocol",
      "findings": [
        {
          "finding": "Combined pushdown filters could exceed tmux format-recursion and command-message limits before dispatch",
          "severity": "high",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "Pinned tmux 3.2a through 3.7b sources define FORMAT_LOOP_LIMIT as 100 and MAX_IMSGSIZE as 16,384; client command packing adds one NUL per UTF-8 argv item and a four-byte command header inside the 16-byte imsg envelope. At 953a1970d91bbe319906a8a2e294799eb4b966ca, QueryPlanner uses immutable target command shapes, balanced conjunctions, a 16,364-byte packed-argv ceiling, whole-predicate Automatic fallback, and Required rejection. Required_mode_balances_101_safe_conjuncts, Packed_argv_budget_counts_exact_utf8_bytes, Balanced_101_conjunct_plan_matches_real_tmux, and Oversized_literal_fails_raw_but_planner_stays_residual cover all three catalogs; all 14 required matrix rows passed 650 tests."
        },
        {
          "finding": "Planner validation could accept contender-only fields that the closed snapshot materializer cannot supply",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At 95e3725d34d7d43fdfc242e8d454dae523a08a64, QuerySemanticValidator validates the closed schema before the contender and requires exact QueryField equality. Catalog_only_ordered_fields_fail_closed_validation and Catalog_only_equality_fields_fail_closed_validation assert that invented physical Int64 and instant fields fail before caller dispatch in Disabled, Automatic, and Required modes for all three catalogs."
        },
        {
          "finding": "Client pushdown and target-command claims were not valid across the required tmux range",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "Pinned cmd-list-clients sources omit -f in tmux 3.2a and 3.3a and include it from 3.4 through 3.7b, while list-sessions, list-windows, and list-panes support the promised filters across the required range. At 953a1970d91bbe319906a8a2e294799eb4b966ca, immutable per-version command profiles make Client Automatic residual and Required reject on 3.2a and 3.3a, then enable Client pushdown from 3.4. Client_typed_id_plan_matches_versioned_filter_support plus the Window and Pane typed-ID real differentials run through every required lane and catalog."
        },
        {
          "finding": "Public capability construction could widen fixed command and protocol safety profiles",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "At 953a1970d91bbe319906a8a2e294799eb4b966ca, QueryListCommandCapability construction is internal and the five-argument QueryPlannerCapabilities constructor is private. The sole public planner constructor derives target commands, Client filter support, the loop limit, and the packed-argv ceiling from the version. Protocol_safety_profiles_cannot_be_supplied_publicly verifies that constructor surface by reflection, and Official_protocol_limits_and_client_support_are_version_derived verifies the fixed profiles."
        },
        {
          "finding": "A copied public plan surface could let callers omit residual evaluation or treat native filters as typed-equivalent",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The bakeoff exposes plan mechanics to measure them but does not establish a production executor boundary. The accepted decision excludes planner infrastructure from public API, requires an internal executor that always applies the residual predicate after candidate materialization, keeps safe plan filters internal, and exposes native tmux filters only through a separately named unsafe operation. Automatic_pushdown_filters_candidates_before_residual_evaluation demonstrates the required candidate-then-residual behavior against the independent reference path in every required lane."
        },
        {
          "finding": "Advisory master does not establish a stable query capability profile",
          "severity": "medium",
          "disposition": "accepted",
          "resolution": "resolved",
          "evidence": "The pinned master cache identifies source 851c5a933d4838c32ad06c248b2ba975d106149c as tmux next-3.8. The matrix passes that reported version to QueryPlannerCapabilities, which rejects it rather than inferring a stable profile; both advisory framework rows therefore failed while retaining a 650-test observation. The decision limits supported claims to tmux 3.2a through 3.7b and records the development-branch capability profile as an unknown."
        }
      ]
    }
  ]
}
```
