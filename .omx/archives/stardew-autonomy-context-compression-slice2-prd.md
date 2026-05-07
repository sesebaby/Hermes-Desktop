# PRD: Stardew Autonomy Context Compression Slice 2

Date: 2026-05-06
Status: Approved for execution by user after reference-first discussion
Context: `.omx/context/context-bloat-cost-20260506T013532Z.md`
Reference: `external/hermes-agent-main`

## Requirements Summary

Reduce Stardew NPC autonomy context bloat without changing NPC game behavior. This slice preserves the existing first-call budget interface, adds a separate per-outbound autonomy compaction interface, adds reference-style tool-pair sanitization, shapes autonomy session state before prompt building, and records provider/estimated token plus DeepSeek RMB cost telemetry.

This is a `title-local adaptation`: we keep deterministic game-frame/message compaction first because Stardew autonomy has structured world state and must stay cheap enough for small local models. We do not copy the full generic LLM summary compressor or session split/writeback in this slice.

## RALPLAN-DR Summary

### Principles

1. Preserve the single `PromptBuilder` / `ContextManager` / `SoulService` runtime chain.
2. Compact outbound autonomy prompts only; do not rewrite `session.Messages` or transcript in this slice.
3. Preserve NPC decision ownership; host compacts facts but does not decide intent.
4. Keep compression deterministic and cheap before adding local models or LLM summaries.
5. Make prompt size and estimated DeepSeek cost observable on every autonomy LLM request.

### Decision Drivers

1. User target: default real player session should trend toward 30 minutes <= 1 RMB.
2. Local 2B-9B models will fail or slow down if prompt bloat remains.
3. Recent commits already reduced prompt text; remaining risk is multi-turn/tool-loop payload and `SessionStateJson` growth.

### Viable Options

#### Option A: Keep first-call budget and add outbound autonomy compaction

Pros: uses the existing runtime seam, keeps first-call semantics intact, avoids second prompt stack, directly fixes later tool-loop bloat.

Cons: not yet a full generic reference `ContextEngine`; session hygiene/writeback remains follow-up.

#### Option B: Port `external/hermes-agent-main` generic `ContextCompressor` and session split now

Pros: closest long-term parity with the reference.

Cons: larger risk, adds LLM summary cost/latency, and crosses current architecture boundaries before deterministic compaction is exhausted.

#### Option C: Shrink prompt/supplement text again

Pros: very low code risk.

Cons: already attempted; `adb96b5f` points next to session state and shared/multi-turn layers.

Chosen: Option A.

## Acceptance Criteria

- Autonomy-marked sessions apply budget compaction before every `CompleteWithToolsAsync` call in `Agent.ChatAsync`, including later tool-loop iterations that use `session.Messages`.
- Non-autonomy sessions and private chat sessions still no-op.
- Compaction affects only outbound message lists, not authoritative `session.Messages` or transcript.
- Remaining tool messages are protocol-safe: no orphan tool result survives without its assistant request, and missing tool results for surviving assistant tool calls get deterministic stub results.
- Session state system blocks are shaped structurally enough to preserve valid JSON semantics; no hard truncation inside JSON fields is used for this slice.
- Logs include `usageSource=provider|estimated`, estimated prompt tokens, actual prompt tokens when available, chars before/after, budget result, and estimated DeepSeek RMB cost.
- No new memory/persona/summary lane is introduced.
- Out of scope: transcript rewrite, session split, generic LLM summary main path, host-side NPC decisions, and hard 30-minute budget enforcement.

## Implementation Steps

1. Add failing tests in `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyContextBudgetTests.cs`:
   - later tool iteration uses policy output before the second `CompleteWithToolsAsync`.
   - surviving tool pairs are sanitized after old assistant/tool pruning.
   - budget logs include estimated cost fields.

2. Update the budget contract in `src/Core/FirstCallContextBudget.cs`:
   - keep `IFirstCallContextBudgetPolicy` as the first-call contract;
   - add `IOutboundContextCompactionPolicy` for every outbound autonomy LLM request.

3. Update `src/runtime/StardewAutonomyFirstCallContextBudgetPolicy.cs`:
   - keep marker-gated no-op behavior;
   - add estimated token/RMB fields to start/completed logs;
   - sanitize tool pairs after pruning by deleting orphan tool results and adding stub tool results for surviving assistant tool calls missing a result.

4. Update `src/Core/Agent.cs`:
   - apply outbound compaction before every autonomy tool-loop request, not only `iteration == 1`;
   - keep `preparedContext` first iteration behavior unchanged;
   - keep streaming tool-loop behavior for follow-up unless tests expose parity need.

5. Add provider usage plumbing:
   - add `UsageStats? Usage` to `ChatResponse` in `src/Core/Models.cs`;
   - parse non-streaming usage in `OpenAiClient` and `AnthropicClient` where available;
   - include actual/estimated usage in `Agent` completion logs.

6. Run focused tests:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyContextBudgetTests|FullyQualifiedName~AgentTests"
```

Then run Desktop build if tests pass.

## Risks And Mitigations

- Risk: applying compaction to later iterations breaks tool protocol. Mitigation: sanitizer tests and deterministic stub/deletion behavior.
- Risk: usage telemetry is mistaken for hard cost enforcement. Mitigation: field names distinguish `estimated` vs `provider`; 30-minute budget enforcement is a later milestone.
- Risk: adding a new engine abstraction creates another local lane. Mitigation: this slice extends the existing policy seam only.
- Risk: `SessionStateJson` remains large. Mitigation: log it now and handle structure-level shaping in the next narrow slice if tests show pressure.

## ADR

Decision: Keep the first-call policy seam, add outbound autonomy compaction for every tool-loop LLM request, add sanitizer/state shaping/telemetry, and defer generic session rewrite/summary.

Drivers: cost target, local-model readiness, reference project lifecycle, current repo seam shape.

Alternatives considered: generic compressor/session split now; prompt-only shrinking; local model before compression.

Why chosen: It is the smallest reference-aligned slice that fixes the known first-call-only gap without creating a second prompt stack.

Consequences: `Agent` gains one new optional outbound compaction dependency. The concrete Stardew policy can implement both contracts while ordinary agents remain no-op.

Follow-ups: persisted `last_prompt_tokens`, session hygiene/writeback, UI cost panel, hard per-session budget gate, LM Studio model routing.

## Staffing Guidance

Solo execution is sufficient. Use `executor` behavior locally; use `verifier` review only if a broad diff appears.

## Verification Path

- Red tests before production changes.
- Targeted `StardewAutonomyContextBudgetTests` and relevant `AgentTests`.
- Desktop build: `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64`.
