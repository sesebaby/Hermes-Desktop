# Test Spec: Hermes Desktop Python Memory Parity

Source plan: .omx\plans\hermes-desktop-memory-parity-initial-plan.md
Reference project: `external/hermes-agent-main` (`D:\Projects\Hermes-Desktop\external\hermes-agent-main`)
Status: consensus-approved by Architect and Critic on 2026-04-27

## Test Objective

Prove that C# Hermes Desktop automatically recalls prior sessions in the Python-style memory boundary model: transcript recall is turn-scoped, current-user augmented at API-call time, shared with session_search, and never persisted as new user content.

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

## Required Verification Commands

- dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore
- Targeted test filters for new recall/coordinator tests once implemented.
- Manual cold-start check using the development app in Desktop\HermesDesktop\bin\x64\Debug\net10.0-windows10.0.26100.0.

## Manual Scenario

1. Start app/session A and ask Hermes to perform or remember a distinctive task/fact.
2. Confirm JSONL transcript is written under %LOCALAPPDATA%\hermes\hermes-cs\transcripts.
3. Restart app or open session B.
4. Ask what was requested earlier.
5. Expected: Hermes answers from recalled transcript context with no manual session_search tool call.
6. Expected: JSONL for session B contains only the user's actual question, not the injected recall block.


