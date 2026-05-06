# Reference Mapping Contract: Dream Reference Memory Alignment

## Scope

This contract covers the reference-aligned memory lifecycle behind the current `dream` completeness request. It does not require a standalone Dreamer module in the reference project because `external/hermes-agent-main` expresses the relevant behavior through memory, session search, and compression lifecycle.

## Reference Anchors

- `external/hermes-agent-main/agent/memory_provider.py`
- `external/hermes-agent-main/agent/memory_manager.py`
- `external/hermes-agent-main/run_agent.py`
- `external/hermes-agent-main/agent/prompt_builder.py`
- `external/hermes-agent-main/tools/memory_tool.py`
- `external/hermes-agent-main/tools/session_search_tool.py`

## Reference Chain Reconstruction

| Layer | Reference behavior |
| --- | --- |
| Trigger | Turn start, tool use, completed turn sync, and context compression pressure. |
| Snapshot | Built-in curated memory reads `MEMORY.md` / `USER.md` as persistent state and exposes a bounded prompt snapshot. |
| Prompt / Summary Assembly | Curated memory is prompt context; `session_search` is separate proactive transcript recall; compression can include provider handoff text. |
| Parse / Repair / Normalize | Memory tool validates and bounds writes; session search normalizes transcript results; compression sanitizes context. |
| Projector / Executor | Memory manager dispatches provider lifecycle hooks and aggregates pre-compression output. |
| Authoritative Writeback | Built-in memory provider and memory tool write durable curated memory; transcript/session search remains transcript-derived. |
| Player-visible Surface | `memory`, `session_search`, and `/compress` expose separate responsibilities. |

## Current Project Mapping

| Layer | Hermes-Desktop landing point |
| --- | --- |
| Trigger | `TurnMemoryCoordinator`, `HermesMemoryOrchestrator`, `ContextManager` |
| Snapshot | `MemoryManager` plus `BuiltinMemoryPlugin` frozen system-prompt snapshot |
| Prompt / Summary Assembly | `PromptBuilder`, `ContextManager`, `TurnMemoryCoordinator.BuildMemoryContextBlock` |
| Parse / Repair / Normalize | `MemoryTool`, `TranscriptRecallService`, `SessionSearchTool` |
| Projector / Executor | New memory compression participant contract plus `HermesMemoryOrchestrator` |
| Authoritative Writeback | `MemoryManager`, `MemoryTool`, `MemoryReviewService` |
| Player-visible Surface | Existing `memory`, `session_search`, Dreamer dashboard/status surfaces |

## Approved Adaptations

| Adaptation | Category | Approval |
| --- | --- | --- |
| Preserve current C# `DreamerService` as a separate background feature instead of replacing it with reference memory internals. | title-local adaptation | Approved by user when approving the ralplan and requesting execution. |
| Keep curated memory frozen per session through `BuiltinMemoryPlugin` instead of making it per-turn provider recall. | equivalent mapping | Approved by user when approving the ralplan and requesting execution. |
| Add a sibling compression participant contract instead of changing every `IMemoryProvider` implementation to carry compression behavior. | title-local adaptation | Approved by user when approving the ralplan and requesting execution. |

## Forbidden Drift

- Do not register or start `AutoDreamService` by default.
- Do not make Dreamer an authoritative memory writer.
- Do not return curated-memory snapshot text from dynamic provider `PrefetchAsync`.
- Do not duplicate curated memory in both plugin prompt blocks and `<memory-context>`.
- Do not collapse `memory` and `session_search` tool responsibilities.

## Execution Rule

The implementation is reference-first at the capability and authority-flow level. If execution requires changing authoritative writeback, canonical memory truth, prompt injection ownership, or writeback order beyond the approved adaptations above, stop and record a new deviation before continuing.

