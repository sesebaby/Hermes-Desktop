# Hermes Desktop Memory Parity Initial Plan

Date: 2026-04-27
Scope: Brownfield parity plan for C# Hermes Desktop memory behavior against `external/hermes-agent-main`
Status: Final RALPLAN consensus candidate after Revision 2 / Architect approval

## Revision 2 Changelog

- Locked the `retrievedContext` contract: it is coordinator-internal transport and budget input only for transcript recall, not a stable prompt emission surface.
- Named a concrete shared coordinator API replacing all current null-wired entry points: `TurnMemoryCoordinator.PrepareFirstCallAsync(sessionId, userMessage, baseMessages, mode, ct)`.
- Split expanded testing by responsibility: `PromptBuilder` verifies stable layers only; coordinator/Agent integration verifies first-call augmentation and non-persistence.
- Chose a non-blocking indexing hook shape: transcript post-write observer/decorator semantics, preserving transcript write success if indexing fails.
- Added execution handoff guidance for `ralph` and `team`, with agent roster, staffing, reasoning levels, launch hints, and verification path.

## Revision 1 Changelog

- Reframed Option A as a thin coordination approach centered on a shared `TranscriptRecallService` or `TurnMemoryCoordinator`.
- Made it mandatory that automatic recall and `session_search` share one recall/search service and one corpus contract.
- Added ADR language declaring transcript JSONL authoritative and SQLite FTS derived and rebuildable.
- Chose Python-parity recall injection lane now: API-call-time augmentation of the current user turn, not a synthetic system layer.
- Moved the stable file-memory vs ephemeral transcript-recall boundary decision into ADR and Phase 2 design.
- Required one shared first-turn preparation path for sync, tool-loop, and streaming entry points.
- Added explicit non-blocking indexing and backfill requirements so transcript persistence cannot fail because indexing fails.
- Expanded verification to cover `CompleteAsync`, first `CompleteWithToolsAsync`, first streaming tool-loop call, and non-persistence of injected recall.

## Evidence Snapshot

- Product claim: cross-session context and compiled memory stack are documented in `ReadmeCn.md:76` and `ReadmeCn.md:93-99`.
- User evidence: transcripts exist, but `%LOCALAPPDATA%\\hermes\\hermes-cs\\memory` and `%LOCALAPPDATA%\\hermes\\memories` were empty during manual recall testing.
- Recent memory commits appear limited to directory/frontmatter/path/migration alignment; no evidence of automatic transcript recall or prompt injection was found.
- Current C# retrieval plumbing exists but is unwired at the agent entry points:
  - `src/Context/ContextManager.cs:59-171` supports `retrievedContext`.
  - `src/Context/PromptBuilder.cs:102-111` currently inserts retrieved context into the outgoing prompt, which is the behavior this plan will narrow for transcript recall.
  - `src/Core/Agent.cs:244-245`, `src/Core/Agent.cs:692-693`, and `src/Core/AgentLoopScaffold.cs:89-93` pass `retrievedContext: null`.
- Current C# memory injection is fragile on the first turn:
  - `src/plugins/BuiltinMemoryPlugin.cs:28-45` returns a transient system block.
  - `src/Core/Agent.cs:203-223` inserts that block into `session.Messages`.
  - `src/Core/Agent.cs:244-245` then builds `preparedContext`, and `src/Core/Agent.cs:280-282` sends `preparedContext` on the first tool-loop iteration, so transient plugin messages can be bypassed.
- Current C# session search is present but passive:
  - `src/search/SessionSearchIndex.cs:15-17` provides an FTS5 index.
  - `src/transcript/TranscriptStore.cs:33-68` does not index writes.
  - `Desktop/HermesDesktop/App.xaml.cs` registers `SessionSearchTool` (`741-744`) but there is no registration/use of `SessionSearchIndex`.
  - `src/Tools/SessionSearchTool.cs:7-10` scans JSONL only when the model voluntarily calls the tool.
- Python reference separates stable system memory from ephemeral recall:
  - `external/hermes-agent-main/run_agent.py:4555-4571` keeps persistent MEMORY/USER snapshots in the system prompt.
  - `external/hermes-agent-main/run_agent.py:9575-9585` prefetches recall once before the tool loop.
  - `external/hermes-agent-main/run_agent.py:9721-9735` injects fenced recall into the current user message at API-call time.
  - `external/hermes-agent-main/run_agent.py:12589-12594` syncs completed turns back to memory providers.
  - `external/hermes-agent-main/hermes_state.py` and `external/hermes-agent-main/tools/session_search_tool.py` provide SQLite FTS5 + summarized recall.

## RALPLAN-DR Summary

### Principles

1. Preserve current C# architecture where it already has the right seams; repair wiring before introducing new subsystems.
2. Match Python prompt-boundary semantics first: stable persistent memory snapshot plus ephemeral per-turn recall injection.
3. Make the first milestone user-visible: cross-session recall must work without the model volunteering a tool call.
4. Keep persistence and recall paths observable and testable because memory failures are silent and regress easily.
5. Separate recall from authority: recalled transcript context is advisory background, not persisted as new user input.

### Top Decision Drivers

1. User-visible parity gap: current Desktop behavior contradicts the README claim and fails the manual recall scenario.
2. Existing C# seams already support retrieved context, but the agent loop does not feed them.
3. Persistence/data behavior is high-risk: regressions can silently drop recall, duplicate context, or poison prompts.

### Viable Options

#### Option A: Thin coordination repair on current C# seams

Summary: Introduce a thin shared coordination layer, `TranscriptRecallService` or `TurnMemoryCoordinator`, that prefetches transcript recall once per turn, provides one shared recall/search service for automatic recall and `session_search`, and drives one first-call preparation path for sync, tool-loop, and streaming entry points without redesigning the whole plugin system.

Pros:
- Smallest diff against the current app.
- Reuses existing context seams with narrowed transcript-recall semantics.
- Delivers the first user-visible recall milestone fastest.
- Creates one explicit owner for transcript recall orchestration without forcing a full provider rewrite.

Cons:
- Requires disciplined boundary handling so stable file-memory and ephemeral transcript recall do not overlap or duplicate.
- Still defers a full multi-provider orchestrator if future evidence shows one owner is needed for all memory providers.

#### Option B: Introduce a C# memory orchestration layer modeled on Python `MemoryManager`

Summary: Add a dedicated orchestrator that owns stable system memory blocks, prefetch recall, post-turn sync, and provider/tool bridging, then route the agent loop through it.

Pros:
- Closest conceptual match to Python.
- Clear long-term ownership boundary for built-in memory, transcript recall, and future providers.
- Reduces prompt assembly ambiguity across plugins, tools, and context manager.

Cons:
- Higher scope and more moving parts before the first parity milestone.
- Greater regression risk in agent loop behavior and plugin lifecycle.
- Harder to land incrementally without temporarily duplicating responsibilities.

#### Option C: Tool-only parity hardening

Summary: Improve `session_search` and memory tools, but keep recall model-driven.

Pros:
- Lowest implementation risk.
- Leaves current agent loop mostly untouched.

Cons:
- Does not solve the reported failure mode.
- Still depends on the model deciding to call a tool.
- Conflicts with the README claim of automatic cross-session memory behavior.

### Recommendation

Choose Option A now, but shape the repair so it can grow into Option B later if needed.

Invalidation rationale for deferring Option B now:
- The current repo already has the necessary retrieval seam (`retrievedContext`) and a stable prompt builder.
- The missing behavior is primarily orchestration and injection timing, not absence of all primitives.
- A larger orchestrator rewrite would delay the first verifiable recall milestone without proving additional value first.

Option A is therefore a thin coordination design, not scattered local wiring:
- `TranscriptRecallService` or `TurnMemoryCoordinator` owns transcript prefetch-once-per-turn.
- The same service owns the shared corpus/search contract used by automatic recall and `session_search`.
- `ContextManager` remains the prompt assembler for stable layers.
- `TurnMemoryCoordinator.PrepareFirstCallAsync(sessionId, userMessage, baseMessages, mode, ct)` becomes the single replacement surface for `src/Core/AgentLoopScaffold.cs:89-93`, `src/Core/Agent.cs:244-245`, and `src/Core/Agent.cs:692-693`; it returns outbound messages plus recall diagnostics and is the only shared first-call preparation entry point.
- A full Python-style multi-provider orchestrator remains deferred unless parity work exposes a real ownership problem.

### Pre-Mortem

1. First-turn recall still fails because retrieval is generated but injected into the wrong message layer, so tool-loop calls still miss it.
2. Recall works but becomes noisy or duplicative because transcript snippets, built-in memory blocks, and session state all repeat the same facts.
3. Persistence appears healthy in tests but production recall stays empty because transcripts are not indexed/backfilled or because the app points at mismatched storage roots.

## ADR Draft

### Decision

Implement Python-style memory parity in Hermes Desktop by repairing the existing C# context path through a thin shared coordination layer: keep persistent file-memory snapshots in stable prompt/system layers, inject transcript recall as ephemeral per-turn API-call-time augmentation of the current user turn, and back automatic recall plus `session_search` with one indexed transcript recall service.

### Drivers

- Deliver automatic cross-session recall that matches documented behavior.
- Preserve current C# architecture where it already exposes the correct seams.
- Minimize prompt-boundary regressions by following Python’s stable-snapshot plus ephemeral-recall model.

### Alternatives Considered

- Full C# memory orchestrator rewrite now.
- Tool-only improvement with no automatic recall.

### Why Chosen

- Fastest path to restore the missing user-visible capability.
- Aligns with both the current C# `ContextManager` design and the Python reference’s prompt semantics.
- Keeps later refactoring optional rather than mandatory for parity.
- Creates a single transcript-recall owner now without prematurely committing to a full orchestrator rewrite.

### Consequences

- Transcript JSONL remains the authoritative conversation store; SQLite FTS is a derived, rebuildable acceleration layer and must never become the sole source of truth.
- A shared `TranscriptRecallService` or `TurnMemoryCoordinator` will own prefetch-once-per-turn behavior, transcript/index access, and shared first-call preparation for sync/tool-loop/streaming paths.
- Stable file-memory vs ephemeral transcript-recall boundaries are fixed in the design:
  - file-memory is stable session/system-layer context,
  - transcript recall is ephemeral current-turn augmentation only.
- `retrievedContext` is explicitly narrowed for transcript recall: it is coordinator-internal transport and budget input only. `PromptBuilder` must not emit transcript recall as a synthetic system message. If `retrievedContext` remains in use for other stable knowledge lanes in the future, that use must stay distinct from transcript recall.
- Transcript writes must succeed even if indexing or index backfill fails; indexing is best-effort and observable.
- Tests must assert message-layer boundaries and persistence exclusions, not just tool availability.

### Follow-Ups

- Re-evaluate whether the plugin model should remain the long-term owner for built-in memory once parity is stable.
- Consider a dedicated recall service/orchestrator abstraction if multiple recall providers emerge.

Future non-transcript retrieval note: the `retrievedContext` narrowing applies specifically to transcript recall parity. Transcript recall must not be emitted as a synthetic system layer. The repo may still reserve `retrievedContext` as a generic future transport for non-transcript retrieval, but any such use must be explicitly separated from transcript recall semantics and tested for prompt-lane isolation.
## Implementation Phases

### Phase 1: Lock the failing behavior with regression tests

Primary files:
- `Desktop/HermesDesktop.Tests/Services/AgentTests.cs`
- `Desktop/HermesDesktop.Tests/Services/TranscriptStoreTests.cs`
- likely new tests near `ContextManager` / recall services

Work:
- Add tests proving the first tool-loop LLM call receives recall context on iteration 1.
- Add tests proving plugin-only transient system injection can be bypassed by `preparedContext` today, then update expectations for the repaired path.
- Add tests for transcript persistence plus recall bootstrap on an existing prior session.

Acceptance criteria:
- A failing test exists for the reported cross-session recall gap before execution changes begin.
- Tests distinguish stable system memory, retrieved context, and current user message boundaries.

Risks:
- Existing tests currently validate plugin system prompt injection only on simple completion calls, not the prepared-context tool loop.

Verification:
- Targeted test runs for `AgentTests`, `TranscriptStoreTests`, and any new recall-specific test class.

### Phase 2: Introduce a C# recall orchestration path

Primary files:
- `src/Core/Agent.cs`
- `src/Core/AgentLoopScaffold.cs`
- `src/Context/ContextManager.cs`
- `src/Context/PromptBuilder.cs`
- a new shared recall coordinator under `src/search/` or `src/Context/`

Work:
- Introduce `TranscriptRecallService` or `TurnMemoryCoordinator` as the shared owner for:
  - prefetch-once-per-turn transcript/session recall,
  - one recall/search service and one corpus contract for automatic recall and `session_search`,
  - one shared first-call preparation path for sync, tool-loop, and streaming entry points.
- Define the exact shared coordinator API now:
  - `TurnMemoryCoordinator.PrepareFirstCallAsync(sessionId, userMessage, baseMessages, mode, ct)`
  - returns: outbound first-call messages plus recall diagnostics/metadata for logging and tests
  - `mode` distinguishes `CompleteAsync`, first `CompleteWithToolsAsync`, and first streaming tool-loop call without forking the preparation contract.
- Use that one method to replace/prep all three current null-wired paths:
  - `src/Core/AgentLoopScaffold.cs:89-93`
  - `src/Core/Agent.cs:244-245`
  - `src/Core/Agent.cs:692-693`
- Add one turn-preparation path that all first calls use; do not maintain separate ad hoc wiring in `ChatAsync` and streaming code.
- Feed non-null transcript recall through coordinator-internal `retrievedContext` transport into `ContextManager.PrepareContextAsync(...)` only for budget accounting and shared prep, not for final synthetic-system emission.
- Choose the ephemeral recall injection lane now:
  - transcript recall is injected as API-call-time augmentation of the current user turn,
  - not as a labeled synthetic system layer.
- Tradeoff rationale:
  - current-user augmentation matches Python parity and keeps turn-scoped recalled context out of stable cached system layers,
  - a synthetic system layer would be easier to bolt onto current C# prompt assembly but would blur the authority boundary and risk being treated as durable/global context instead of per-turn background.
- Preserve boundary semantics:
  - persistent curated file-memory remains stable across the session,
  - transcript/session recall is ephemeral for the current turn only,
  - recalled context is fenced/labeled as background rather than user-authored text.
- Lock the `retrievedContext` contract explicitly:
  - for transcript recall, `retrievedContext` is coordinator-internal transport and budget input only,
  - `PromptBuilder` must not emit transcript recall as a system message,
  - the coordinator augments only the outbound current-user message for the first API call,
  - the coordinator must not mutate persisted `session.Messages` when doing this augmentation.

Acceptance criteria:
- A new session can recall relevant details from a prior transcript without a model-initiated `session_search` tool call.
- The same coordinator prepares the first outbound call for `CompleteAsync`, first `CompleteWithToolsAsync`, and the first streaming tool-loop call.
- The first LLM tool-loop request contains retrieved recall context.
- Recalled context is not written back into the transcript as if it were new user content.

Risks:
- Duplicating memory content across `BuiltinMemoryPlugin` and coordinator-managed transcript recall.
- Breaking cache/prompt ordering assumptions in `PromptBuilder`.
- Accidentally diverging sync vs streaming first-turn behavior if the shared prep path leaks.

Verification:
- Unit tests on prompt assembly.
- Message-capture tests around `CompleteAsync`, first `CompleteWithToolsAsync`, and the first streaming tool-loop iteration.

Recall diagnostics sink: `TurnMemoryCoordinator.PrepareFirstCallAsync(...)` returns structured recall diagnostics to the caller. The caller records them through existing application logging and test-observable result metadata; diagnostics include attempted/skipped status, source counts, injected character/token estimate, index bootstrap status, and empty-recall reason.
### Phase 3: Wire transcript indexing and retrieval source quality

Primary files:
- `src/transcript/TranscriptStore.cs`
- `src/search/SessionSearchIndex.cs`
- `Desktop/HermesDesktop/App.xaml.cs`
- `src/Tools/SessionSearchTool.cs`

Work:
- Register `SessionSearchIndex` in DI and connect transcript writes to indexing through a non-blocking post-write observer/decorator shape.
- Preferred implementation approach:
  - `TranscriptStore` accepts an optional index observer/event or is wrapped by a decorator that reacts after write success,
  - indexing must not sit inline on the critical success path of transcript persistence,
  - observer/decorator failures are logged and surfaced via diagnostics but cannot fail the transcript write.
- Make transcript JSONL authoritative and FTS derived:
  - transcript writes succeed even if indexing fails,
  - index bootstrap/backfill failures are logged and recoverable,
  - the FTS database can be rebuilt from JSONL transcripts.
- Add index backfill/bootstrap for existing JSONL transcripts so prior sessions are searchable without waiting for new writes.
- Automatic recall and `session_search` must share one recall/search service and one corpus contract.
- Decide whether the shared service returns raw snippets first or summarized recall; default to bounded raw/snippet retrieval for milestone 1, while preserving the ability to add summarization later behind the same service boundary.

Acceptance criteria:
- Existing transcript history becomes searchable after app startup or an explicit bootstrap path.
- New transcript writes are indexed automatically.
- Automatic recall and manual `session_search` use the same underlying recall/search service and corpus contract.
- Transcript persistence succeeds even when indexing/backfill fails.

Risks:
- Bootstrap cost on first run with large transcript history.
- FTS query sanitization in `SessionSearchIndex.cs:145-154` is currently minimal.
- Hidden drift between JSONL and FTS if rebuild semantics are not explicit and test-covered.

Verification:
- Index bootstrap tests with seeded JSONL transcripts.
- Persistence tests verifying `SaveMessageAsync` triggers searchable entries.
- Failure-injection tests proving transcript writes still succeed when indexing fails.

### Phase 4: Reconcile built-in memory snapshot vs per-turn recall

Primary files:
- `src/plugins/BuiltinMemoryPlugin.cs`
- `src/memory/MemoryManager.cs`
- `src/Core/Agent.cs`
- `Desktop/HermesDesktop/App.xaml.cs`

Work:
- Remove or reduce transient plugin injection paths that are semantically wrong for prepared-context first turns.
- If built-in file memory remains plugin-driven, ensure it is included in the prepared prompt path instead of only mutating `session.Messages`.
- Keep file-memory behavior aligned with the ADR/Phase 2 boundary rather than re-deciding it here.

Acceptance criteria:
- No first-turn memory loss due to plugin injection order.
- Curated memory writes and transcript recall do not compete for the same prompt lane.
- Memory behavior is consistent between no-tool, tool-loop, and streaming flows.

Risks:
- Over-correcting into a larger orchestrator rewrite.
- Changing session-long memory behavior unintentionally.

Verification:
- Cross-path tests for simple completion, tool loop, and streaming.
- Manual verification with saved file memory plus prior transcript recall in the same conversation.

### Phase 5: User-visible parity pass and observability hardening

Primary files:
- `Desktop/HermesDesktop/App.xaml.cs`
- logging/diagnostic touchpoints in recall and transcript services
- `ReadmeCn.md` follow-up only if behavior/wording still diverges after implementation

Work:
- Add structured logs/metrics for recall fetch count, source type, index bootstrap status, and empty-recall reasons.
- Add a manual verification script/checklist for the exact user scenario:
  - session A stores facts in transcript,
  - app restarts,
  - session B asks for prior fact,
  - agent recalls it automatically.
- Reconcile docs only after behavior matches reality.

Acceptance criteria:
- User-visible cross-session recall works in a cold-start manual test.
- Engineers can diagnose empty recall via logs without inspecting disk manually.

Risks:
- False confidence from tests if runtime storage roots differ from test paths.
- Non-blocking indexing may hide failures if observability is weak.

Verification:
- Manual cold-start recall test.
- Review of log evidence for index bootstrap and retrieved-context injection.

## Expanded Test Plan

### Unit

PromptBuilder responsibility:
- `PromptBuilder` verifies stable layers only.
- `PromptBuilder` continues to emit stable system/session layers correctly.
- `PromptBuilder` tests explicitly assert transcript recall is absent from synthetic system layers.

Coordinator/service responsibility:
- Recall context is labeled/fenced as informational background when augmented into the current user turn.
- `SessionSearchIndex` indexes, searches, deletes, and sanitizes queries correctly.
- Shared recall service exposes one corpus/search contract for automatic recall and `session_search`.
- Any recall selector service trims results to bounded prompt budget.
- `MemoryManager` and built-in memory snapshot logic do not duplicate transcript recall.

### Integration

- Coordinator/Agent responsibility:
- `TurnMemoryCoordinator.PrepareFirstCallAsync(...)` augments the first outbound current-user message for `CompleteAsync`.
- `TurnMemoryCoordinator.PrepareFirstCallAsync(...)` augments the first outbound current-user message for the first `CompleteWithToolsAsync` call.
- `TurnMemoryCoordinator.PrepareFirstCallAsync(...)` augments the first outbound current-user message for the first streaming tool-loop call.
- Injected transcript recall never lands in persisted transcript rows or rebuilt FTS rows as if it were user-authored content.

Persistence/indexing responsibility:
- `TranscriptStore.SaveMessageAsync()` causes new messages to become searchable.
- `TranscriptStore.SaveMessageAsync()` still succeeds when indexing throws or backfill is unavailable.
- Existing transcript backfill populates the index on startup.
- Built-in memory plus transcript recall coexist without prompt corruption.

### End-to-End

- Manual user flow with two sessions and app restart demonstrates automatic cross-session recall.
- Tool-call path still works when the first assistant response requires tools.
- Manual `session_search` results remain consistent with automatic recall results.

### Observability

- Log when recall was attempted, what source contributed, how many items were injected, and why recall was empty.
- Log index bootstrap counts and duration.
- Add guardrail logs when recall is skipped due to budget or path/config mismatch.

## Risks and Mitigations

- Prompt duplication risk:
  - Mitigation: assert exact layer ordering in tests and keep transcript recall ephemeral.
- Storage-root mismatch risk:
  - Mitigation: centralize path construction and test against the same path helpers used by app startup.
- Large-history bootstrap risk:
  - Mitigation: incremental/bootstrap-once strategy with telemetry on index size and duration.
- Search quality risk from raw snippet retrieval:
  - Mitigation: milestone 1 can use bounded snippets; milestone 2+ can add summary synthesis if needed.

## First Executable Milestone

User-visible milestone:
- After a prior session transcript exists, a new session asks about earlier work and Hermes Desktop recalls it automatically on the first response, without requiring a manual `session_search` tool call.

Minimum implementation slice for that milestone:
- Regression tests.
- Shared `TranscriptRecallService`/`TurnMemoryCoordinator`.
- Retrieval orchestration into `retrievedContext` plus current-user augmentation at API-call time.
- First-turn prompt injection fix.
- Transcript indexing/bootstrap sufficient for prior-session lookup.

## Execution Handoff

### Available Agent Types

- `executor` — implementation across Phases 2-4
- `test-engineer` — regression, unit, integration, and harness work
- `verifier` — cold-start/manual/log validation and final evidence review
- `architect` — optional for design review if coordinator boundary drifts
- `debugger` — optional if recall still fails after wiring/index bootstrap

### Ralph Staffing Guidance

- Default lane:
  - `executor` with medium-to-high reasoning for Phases 2-4
  - consult `test-engineer` artifacts as checkpoints
  - finish with `verifier`
- Best use:
  - sequential implementation where one owner must preserve the shared coordinator contract and keep sync/stream paths unified.

### Team Staffing Guidance

- Minimum team:
  - `executor` for Phases 2-3
  - `test-engineer` for Phase 1 and expanded verification
  - `verifier` for Phase 5 and final evidence
- Optional additions:
  - `architect` for coordinator API drift or prompt-boundary disputes
  - `debugger` for indexing/rebuild/recall mismatches

### Suggested Reasoning Levels By Lane

- `executor`: high
- `test-engineer`: medium
- `verifier`: high
- `architect`: high
- `debugger`: high

### Launch Hints

- `ralph` path:
  - `proceed`
  - then approve sequential execution with `ralph`
- `team` path:
  - `proceed`
  - then approve implementation with `team`
  - suggested staffing: `executor`, `test-engineer`, `verifier`

### Team Verification Path

- `test-engineer` locks failing behavior first and owns expanded automated coverage.
- `executor` implements the coordinator contract, indexing hook, and prompt-boundary changes.
- `verifier` runs the cold-start two-session manual test, reviews logs for bootstrap/empty-recall reasons, and confirms injected recall never persists into transcript/FTS artifacts.
- If verification fails:
  - route prompt-boundary issues to `architect`
  - route runtime recall/index mismatches to `debugger`

## Review Notes for Architect

- The key decision is now narrowed to thin shared coordination now versus full orchestrator later.
- This draft recommends seam repair plus a shared coordinator first, with the authoritative/derived storage contract, injection lane, and sync/stream unification fixed explicitly up front.
- Ready for Architect/Critic re-review.


