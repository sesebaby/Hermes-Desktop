# Desktop Todo Effect Parity RALPLAN

## Status

Consensus approved on 2026-04-30.

- Planner: revised after reference matrix and Architect feedback
- Architect: approved iteration 2
- Critic: approved

This plan is implementation-ready, but this `$ralplan` turn stops at the plan.

## RALPLAN-DR

### Principles

1. Persisted `todo` tool result JSON is the canonical task truth.
2. Any task archive/history surface is derived, read-only, rebuildable, and must never feed prompt injection or mutate active tasks.
3. Preserve reference semantics: todo order is priority, only active tasks are reinjected, completed/cancelled tasks are not reintroduced as active work, and incomplete archives remain visible as incomplete.
4. Pure UI shape/style differences are allowed as a title-local adaptation: WinUI layout, colors, glyphs, and controls do not need to match the TUI reference.
5. Keep scope desktop-only and avoid premature `TaskManager`/Coordinator coupling.

### Top Decision Drivers

1. The current desktop implementation is mostly aligned on tool execution, session scoping, transcript projection, hydration, and active-task prompt injection.
2. The remaining semantic gaps are model-facing schema guidance, missing id/content test coverage, live panel priority order, and a user-inspectable per-turn task-history equivalent.
3. Duplicate persistence paths exist, so any archive/history read model must be idempotent and must not become a second source of truth.

### Viable Options

#### Option A: Remove Avoidable Semantic Deviations

Implement the missing semantic pieces: fuller schema guidance, source-order panel rendering, and derived task-history/archive read model.

Pros:
- Strongest same-effect parity.
- Avoids pending controlled deviations for core semantics.
- Keeps reference authority intact.

Cons:
- Requires new archive/history derivation tests and a small desktop surface.

#### Option B: Keep Current UI Behavior And Defer Archive

Keep active-first panel sorting and rely on persisted tool JSON plus live panel only.

Pros:
- Smaller patch.

Cons:
- Cannot honestly claim same-effect parity because reference order-as-priority and per-turn inspectability are missing.
- Would need explicit controlled-deviation approval.

#### Option C: Bridge Todos Into `TaskManager`

Map session todos into durable desktop task infrastructure.

Pros:
- Reuses existing durable task concepts.

Cons:
- Diverges from the reference session-scoped todo semantics.
- Couples ordinary chat planning to Coordinator-era task lifecycle too early.

### Chosen Option

Option A.

## Reference Chain

| Layer | Reference Behavior | Desktop Mapping |
| --- | --- | --- |
| Trigger | Model calls single `todo` tool; omitting `todos` reads. | `TodoTool` is model-facing; `todo_write` is hidden compatibility alias. |
| Snapshot | One ordered todo store per agent/session. | `SessionTodoStore` keyed by desktop session id. |
| Prompt / Summary Assembly | Inject pending/in_progress todos after compression only. | `ContextManager` -> `PromptBuilder.ActiveTaskContext`. |
| Parse / Normalize | Missing id -> `?`; missing content -> `(no description)`; invalid status -> `pending`; duplicate ids keep last occurrence. | `SessionTodoStore.Validate` and `DeduplicateById`. |
| Executor | `todo_tool` mutates store and returns full list plus summary. | `TodoTool.ExecuteAsync`. |
| Authoritative Writeback | Full `tool.complete` result is source of truth; tool-start args are not. | Persisted transcript tool messages observed by `SessionTaskProjectionService`. |
| Player-visible Surface | Live todo panel plus per-turn transcript trail. | WinUI live task panel plus derived task-history/archive surface. |

## Implementation Plan

### Commit 1: Lock Reference Semantics With Tests

- Add tests for missing id/content normalization.
- Add schema guidance parity tests for reference behavioral instructions.
- Add panel source-order test.
- Add archive derivation tests before implementation:
  - one archive row for duplicate persistence
  - archive hydration from persisted tool messages
  - malformed todo messages ignored
  - completed-only archive collapsed/cleared from live active state
  - incomplete archive flagged

### Commit 2: Tighten `todo` Tool Contract

- Expand `TodoTool.Description` to include reference-equivalent guidance:
  - use for complex 3+ step or multi-task work
  - read by omitting params
  - replace/merge semantics
  - list order is priority
  - only one item `in_progress`
  - mark completed immediately
  - cancel failed items and add revised item
  - every call returns the full current list
- Keep `todo_write` executable as hidden legacy alias only.

### Commit 3: Preserve Priority Order In Live Panel

- Change `SessionTaskPanelModel` to render authoritative snapshot order.
- Do not sort active items before done items unless later approved as a controlled semantic deviation.
- Pure labels/colors/layout remain desktop-local UI choices.

### Commit 4: Add Derived Task-History Read Model

- Add an archive/history derivation service that scans transcript messages.
- Canonical truth remains persisted `todo` tool JSON.
- The archive is read-only and rebuildable.
- Do not reuse `SessionTaskProjectionService.HydrateSessionAsync` for archive construction because it mutates `SessionTodoStore`.
- Keep archive output separate from `FormatActiveTasksForInjection`.

### Commit 5: Idempotence And Desktop Surface

- Archive exactly once per assistant completion boundary.
- Preferred key if available: `sessionId + assistantMessageId`.
- Current fallback key: `sessionId + latest todo tool-call/content hash + assistant completion boundary hash`.
- Deduplicate duplicate todo tool results and duplicate assistant boundaries.
- Surface the derived archive as either a transcript/history row or a task-history section:
  - completed/cancelled-only rows are collapsed or marked inactive
  - pending/in_progress rows are visibly incomplete
  - source order is preserved

### Commit 6: Verification And Cleanup

- Run focused tests for todo tool, projection, panel model, archive derivation, chat-service lifecycle, and context injection.
- Run the full desktop test project.
- Commit each small verified patch with Lore trailers.

## Acceptance Criteria

1. Model-facing tool definitions advertise `todo`, not `todo_write`.
2. `todo` read/write/merge returns full current list plus summary.
3. Missing id/content and invalid status normalize like the reference.
4. Live task panel preserves todo source order as priority order.
5. Completed/cancelled-only todos are not reinjected into prompt context.
6. Tool completion updates live state from persisted tool result JSON only.
7. Session reload hydrates from latest valid `todo` result, not args or summaries.
8. Task history/archive can be deleted and rebuilt from transcript data.
9. Duplicate persistence produces one archive row.
10. Malformed todo messages do not crash replay or mutate active state.
11. Incomplete archived snapshots are marked incomplete.
12. Pure WinUI visual differences are accepted and do not block parity.

## Verification Plan

Run focused tests first:

```powershell
dotnet test Desktop/HermesDesktop.Tests/HermesDesktop.Tests.csproj -c Debug -p:Platform=AMD64 --filter "TodoToolTests|SessionTaskProjectionServiceTests|SessionTaskPanelModelTests|HermesChatServiceTaskLoopTests|TaskContextInjectionTests"
```

Then run the full desktop suite:

```powershell
dotnet test Desktop/HermesDesktop.Tests/HermesDesktop.Tests.csproj -c Debug -p:Platform=AMD64 -p:UseSharedCompilation=false
```

Manual verification:

- create todos in chat
- confirm panel source order
- complete/cancel todos
- reload session and confirm live state plus task history
- confirm no stale active todos after reset/new session

## Deviation Decisions

Approved:

- WinUI visual form may differ from TUI form.
- Session-keyed singleton store is an equivalent desktop mapping.
- Transcript observer/hydration is an equivalent desktop mapping.

Still not approved if omitted:

- preserving todo order as priority
- user-inspectable task-history/archive equivalent
- active-only prompt reinjection semantics

## ADR Draft

### Title

Adopt a derived, reference-faithful desktop todo history for session task parity

### Status

Proposed; consensus approved for implementation.

### Context

The reference project uses a session-scoped `todo` store, full-list tool results,
active-only prompt reinjection, live todo panel updates, and per-turn transcript
todo archives. The desktop project now matches most tool and projection semantics
but still needs source-order rendering, fuller tool guidance, and a desktop
equivalent of per-turn inspectability.

### Decision

Keep persisted `todo` tool result JSON as the only canonical task source. Add a
derived, rebuildable desktop task-history read model for player-visible archive
semantics. Preserve source order in live and archived views. Accept WinUI visual
differences as title-local adaptation.

### Alternatives

- Direct `TaskManager` mapping: rejected because it changes session todo semantics.
- Deferring archive: rejected unless explicitly approved because it prevents
  same-effect parity.
- Persisting archive as canonical state: rejected because it creates a second
  source of truth.

### Consequences

Positive:
- Same-effect reference parity without copying TUI runtime.
- Clear authority boundary.
- Better reload/replay diagnostics for task progress.

Negative:
- Adds a derived read model and tests.
- Needs idempotence around duplicate message persistence.

### Follow-Ups

- After parity hardening, decide whether durable `TaskManager` tasks should be
  shown alongside session todos as a separate grouped view.
- If `Message` later gains stable ids, migrate archive keys to
  `sessionId + assistantMessageId`.

## Agent Roster And Execution Guidance

Available roles:

- `executor`: implementation patches
- `test-engineer`: focused regression tests and archive edge cases
- `architect`: design review for archive/read-model boundary
- `critic`: final plan/implementation challenge
- `verifier`: test and evidence review
- `git-master`: commit hygiene if needed

Recommended `$ralph` path:

- Single-owner sequential execution.
- Use high reasoning for archive/idempotence commits.
- Commit after each green focused patch.

Recommended `$team` path:

- Lane 1 `test-engineer`: tests for schema, normalization, panel order, archive derivation.
- Lane 2 `executor`: tool description and panel order after tests land.
- Lane 3 `executor`: derived task-history service and desktop hydration.
- Lane 4 `verifier`: run focused tests, full desktop suite, and check git diff.

Suggested launch hint:

```text
$ralph execute .omx/plans/desktop-todo-effect-parity-ralplan-20260430.md
```

or, for parallel execution:

```text
$team execute .omx/plans/desktop-todo-effect-parity-ralplan-20260430.md
```
