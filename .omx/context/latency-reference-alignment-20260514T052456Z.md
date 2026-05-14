# Latency Reference Alignment Preflight

## Task Statement

User observed that AI replies are very slow even when using a local model. User rejected a rough fix and requested `$deep-interview`: read `external/hermes-agent-main` first, then discuss how to fix latency in Hermes-Desktop.

## Desired Outcome

Produce a source-faithful diagnosis and repair direction for reply latency, especially context compression / memory / prompt assembly latency, before any runtime code changes.

## Stated Solution

Align with `D:\Projects\Hermes-Desktop\external\hermes-agent-main` instead of applying blunt mitigations such as disabling summarization or arbitrarily increasing intervals.

## Probable Intent Hypothesis

The user wants the desktop project to preserve the reference project's architectural semantics:

- Compression should be a bounded context-engine behavior, not an unconditional per-turn tax.
- Local models should not be blocked by avoidable pre-answer summarization or background review work.
- Any latency fix should preserve the reference chain and be explicit about deviations.

## Known Facts / Evidence

### Reference Project

- `external/hermes-agent-main/agent/context_engine.py:18-24` defines a context engine lifecycle: instantiate, update token usage after API response, check `should_compress()` after each turn, then call `compress()` only when needed.
- `external/hermes-agent-main/agent/context_compressor.py:276-285` documents the default algorithm: prune old tool results, protect head, protect tail by token budget, summarize middle turns, and iteratively update previous summary.
- `external/hermes-agent-main/agent/context_compressor.py:329-375` derives threshold, tail token budget, and summary budget from actual model context length.
- `external/hermes-agent-main/agent/context_compressor.py:407-427` compresses only when tracked or supplied prompt tokens exceed threshold, with anti-thrashing after ineffective compression.
- `external/hermes-agent-main/agent/context_compressor.py:807-823` calls `call_llm(task: "compression")`, using an auxiliary compression lane rather than blindly using the normal chat turn.
- `external/hermes-agent-main/agent/context_compressor.py:1159-1235` performs cheap pruning, boundary selection, logging, and only then summary generation.
- `external/hermes-agent-main/run_agent.py:8083-8131` wraps compression in `_compress_context`, calls memory `on_pre_compress(messages)` first, then rebuilds system prompt and persists the compressed session path.
- `external/hermes-agent-main/run_agent.py:9442-9500` performs preflight compression only when rough request tokens exceed the compressor threshold, and allows up to three passes for oversized sessions.
- `external/hermes-agent-main/website/docs/developer-guide/context-compression-and-caching.md:79-94` documents compression config under `compression` and auxiliary summarization provider under `auxiliary.compression`.
- `external/hermes-agent-main/website/docs/developer-guide/context-compression-and-caching.md:152-154` warns that the compression model must have enough context because the middle section is sent to `call_llm(task: "compression")`.

### Current Hermes-Desktop

- `src/Context/ContextManager.cs:103-106` always trims transcript to a fixed recent-turn window and treats older messages as evicted.
- `src/Context/ContextManager.cs:141-157` summarizes evicted messages when evicted messages exist and either pressure is high, the summary is empty, or the summary is stale beyond 10 turns.
- `src/Context/ContextManager.cs:407-455` summarizes evicted messages by calling `_chatClient.CompleteAsync(...)`, which is the active chat client, not a dedicated auxiliary compression lane.
- `src/Context/ContextManager.cs:412-417` truncates the evicted transcript to the newest 4000 characters before summarization.
- `src/Context/PromptBuilder.cs:80-139` injects soul context, system prompt, plugin context, session state, active tasks, recent turns, and current user message into every prompt.
- `Desktop/HermesDesktop/App.xaml.cs:590-608` registers `TokenBudget(maxTokens: 8000, recentTurnWindow: 6)` and one `ContextManager` with the primary `IChatClient`.
- `src/Context/TokenBudget.cs:30-34` treats 75% of 8000 as high pressure and 94% as critical pressure.
- `src/compaction/CompactionSystem.cs:10-31` has a separate compaction manager concept, but `rg` found no production registration/call site for `CompactionSystem` or `CompactPartialAsync`.

## Reference Chain Reconstruction

### 1. Trigger

Reference: compression triggers from context-engine pressure checks using actual or rough request tokens, not merely because older turns exist.

Current: summarization can trigger whenever fixed-window eviction exists and summary is empty/stale, even if prompt pressure is otherwise normal.

### 2. Snapshot

Reference: full message list enters compressor; head and tail are protected, and middle turns are selected by token-budget boundary.

Current: transcript is first split into a fixed recent-turn window and evicted messages. Summary input is already truncated to newest 4000 characters.

### 3. Prompt / Summary Assembly

Reference: structured handoff prompt with active task, progress, remaining work, critical context, sensitive-data redaction, iterative previous-summary update, and auxiliary `task: "compression"` routing.

Current: compact structured template exists but is much smaller and routed through the active chat client.

### 4. Parse / Repair / Normalize

Reference: old tool outputs are pruned before summarization, tool call/result boundaries are protected, summary prefix frames compacted turns as reference-only context, and orphaned tool result pairs are sanitized.

Current: summary is plain `state.Summary.Content`; current inspected path does not show equivalent pre-summary tool-output pruning or summary prefix semantics in `ContextManager`.

### 5. Projector / Executor

Reference: compressor returns a new OpenAI-format message list, inserts/merges summary with role alternation safety, appends active todo snapshot, rebuilds system prompt, and can persist a new split session.

Current: prompt builder projects state summary plus recent turns each turn; the summary lives inside `SessionState`.

### 6. Authoritative Writeback

Reference: compression is part of context engine/session lifecycle and calls memory `on_pre_compress` before dropping context.

Current: memory/plugin pre-compress hooks exist, but the main summary state appears in in-memory `SessionState`; persistence boundary needs more inspection before claiming parity.

### 7. Player-visible Surface

Reference: compression is an internal context-space operation; user-facing output remains the actual answer.

Current: user-facing symptom is long reply latency before the answer, likely because pre-answer summarization can run on the same local model/client.

## Constraints

- Do not implement before the interview narrows the acceptable mapping depth.
- Do not describe a repo-local redesign as reference-aligned unless each reference layer maps cleanly.
- Do not preserve dual or shadow execution lanes if they conflict with project constraints.
- No new dependencies unless explicitly approved.

## Unknowns / Open Questions

- Should the first repair aim for full source-faithful `ContextEngine` parity, or a narrower parity patch targeted at the observed latency?
- Is the user willing to add a configured auxiliary compression route in Hermes-Desktop, or must the first pass avoid new routing/config surface?
- Should NPC autonomy sessions use the same compression policy as desktop chat, or a title-local adaptation?
- Should current `src/compaction/CompactionSystem.cs` be retired, integrated, or ignored as unused legacy?

## Decision-Boundary Unknowns

- Deviation category for a C# adaptation of Python `ContextCompressor`: equivalent mapping, title-local adaptation, controlled deviation, or repo-local custom implementation.
- Whether preserving reference writeback order requires session splitting/persistent compressed history in desktop, or whether `SessionState` is acceptable only as a temporary bridge.
- Whether disabling/stretching summaries is explicitly rejected as a fix, or allowed only as a diagnostic/temporary switch.

## Likely Codebase Touchpoints

- `src/Context/ContextManager.cs`
- `src/Context/TokenBudget.cs`
- `src/Context/PromptBuilder.cs`
- `src/compaction/CompactionSystem.cs`
- `src/Memory/*`
- `src/runtime/NpcRuntimeContextFactory.cs`
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop.Tests/**`

## Interview State

- Profile: standard
- Brownfield context: reference-first latency diagnosis
- Current ambiguity estimate: 0.62
- Mandatory gates unresolved: non-goals, acceptable deviation category, success criteria
