Task statement
- Plan a safe first implementation pass for Stardew autonomy context reduction.
- Scope only items 1 and 2:
  - shrink shared `SystemPrompt`
  - shrink `SoulContext`
- Do not implement yet.

Desired outcome
- Produce a concrete single-track implementation plan that reduces first-call autonomy context size without changing NPC behavior semantics.
- Preserve the existing shared `PromptBuilder` architecture.

Known facts / evidence
- Latest runtime logs show autonomy supplement is already reduced to about 2000 chars.
- Remaining first-call overage is in the shared system layer.
- New packet logs show:
  - `systemPromptChars` about 6682-6685
  - `soulContextChars` about 1331-1373
  - `pluginSystemContextChars` about 2598-2800
  - `sessionStateChars` about 625-1780
- Budget logs show `systemChars` about 8544-9845, while builtin memory is classified separately.
- Therefore, the main remaining contributors are:
  - shared `SystemPrompt`
  - `SoulContext`
  - `SessionStateJson`
- Current request is to plan only the first two.

Constraints
- Chinese reporting.
- No dual-track or shadow prompt architecture.
- Keep one shared `PromptBuilder` / prompt assembly framework.
- Must not harm NPC behavior.
- Prefer tool-first / retrieval-first behavior over stuffing prompt context.
- Do not reintroduce mandatory skill body injection.

Unknowns / open questions
- Exact minimum safe autonomy system prompt surface.
- Which `SoulContext` sections are behavior-critical versus just nice-to-have.
- Whether some behavior regressions are currently masked by verbose system guidance.

Likely code touchpoints
- `src/Core/SystemPrompts.cs`
- `src/Core/MemoryReferenceText.cs`
- `src/runtime/AgentCapabilityAssembler.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`
- `src/soul/SoulService.cs`
- relevant tests under `Desktop/HermesDesktop.Tests/`
