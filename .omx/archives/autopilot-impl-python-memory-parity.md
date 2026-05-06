# Autopilot Implementation Plan: Python Memory Parity

## Phase 1 - Tests First
- Add tests for lifecycle orchestrator failure isolation and provider call order.
- Add Agent integration test proving completed turns call memory sync/queue.
- Add session_search tests for empty-query recent sessions and query summaries.
- Add sanitation test for old injected context blocks.

## Phase 2 - Lifecycle Orchestrator
- Add `IMemoryProvider` and `HermesMemoryOrchestrator` under `src/memory`.
- Add `TranscriptMemoryProvider` wrapping `TranscriptRecallService`.
- Update `TurnMemoryCoordinator` to prefetch through the orchestrator while preserving existing direct-constructor compatibility.
- Add `SyncCompletedTurnAsync` and call it from Agent turn completion paths.

## Phase 3 - Session Search Parity
- Extend `TranscriptRecallService` with recent-session listing, session grouping, conversation formatting, sanitation, and summary DTOs.
- Extend `SessionSearchTool` parameters to support optional query, role_filter, and limit while keeping MaxResults compatibility.
- Use optional `IChatClient` summarization with deterministic excerpt fallback.

## Phase 4 - DI and Verification
- Wire orchestrator/provider in Desktop DI.
- Re-run targeted tests, full test project, solution build, and x64 Debug build.
- Record remaining deviations from Python reference.
