# PRD: Hermes Desktop Python Memory Parity

Source plan: .omx\plans\hermes-desktop-memory-parity-initial-plan.md
Status: consensus-approved by Architect and Critic on 2026-04-27

## Requirements Summary

Hermes Desktop C# must align its user-visible memory behavior with the Python `external/hermes-agent-main` reference: prior session facts and task context should be recalled automatically on the first relevant response in a new session, without relying on the model to voluntarily call session_search.

The approved architecture is a repair-first path with a thin shared coordinator, not a full rewrite. Transcript JSONL remains authoritative. SQLite FTS is a derived, rebuildable acceleration layer. Transcript recall is injected by API-call-time augmentation of the outbound current user turn, never as persisted user content and never as a synthetic system message emitted by PromptBuilder.

## Scope

In scope:
- Add TurnMemoryCoordinator.PrepareFirstCallAsync(sessionId, userMessage, baseMessages, mode, ct) or equivalent.
- Replace current null-wired first-call paths in src/Core/AgentLoopScaffold.cs:89-93, src/Core/Agent.cs:244-245, and src/Core/Agent.cs:692-693.
- Make automatic recall and manual session_search share one recall/search service and one corpus contract.
- Add non-blocking transcript indexing/backfill from authoritative JSONL transcripts.
- Preserve streaming/non-streaming/tool-loop parity.
- Add diagnostics for recall attempted/skipped/injected/empty states.

Out of scope for first execution:
- Full Python runtime rewrite.
- Full external memory-provider ecosystem parity.
- UI redesign.
- Making SQLite FTS the authoritative transcript store.

## Acceptance Criteria

1. A new session can answer a question about a prior session using automatic transcript recall without requiring a model-initiated session_search call.
2. First outbound CompleteAsync, first CompleteWithToolsAsync, and first streaming tool-loop call include current-user augmented recall when relevant.
3. Transcript recall is not emitted as a synthetic system message by PromptBuilder.
4. Injected recall is not persisted into JSONL transcript rows as user-authored text.
5. New transcript writes remain successful even if indexing fails.
6. Existing JSONL transcripts can be backfilled into the derived FTS index.
7. Manual session_search and automatic recall use the same recall/search service and corpus contract.
8. Logs/diagnostics explain recall attempts, empty-recall reasons, source counts, and index bootstrap status.

## ADR

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

## Implementation Plan

See canonical plan: .omx\plans\hermes-desktop-memory-parity-initial-plan.md

