# Context Snapshot: context-bloat-cost

Task statement: 用户认为当前 Stardew NPC autonomy 的首轮和多轮上下文膨胀导致成本不可接受；昨天简单测试花费 20 多美元，玩家不能承受。用户希望参考 `external/hermes-agent-main` 解决首轮和多轮上下文膨胀问题。

Desired outcome: 明确下一步应优先处理上下文成本问题还是继续移动可靠性，并把问题收敛成可执行规格。

Stated solution: 参考 `external/hermes-agent-main` 的 context compression / session hygiene / token tracking 思路，解决首轮和多轮上下文膨胀。

Probable intent hypothesis: 用户最关心的是真实玩家可承担成本；如果每个 NPC autonomy tick 或私聊多轮都把大型 system/soul/skill/history/tool result 重复发送，村庄模式无法成立。

Known facts/evidence:
- Recent commits include `4b8e29ed 压缩上下文,第一次尝试失败`, `399ae7f1 压缩上下文,未达标`, `7eada6ac Stop misclassifying ordinary autonomy prompts as preserved task context`, and `adb96b5f Reduce Stardew autonomy system context before first-call budgeting`.
- Commit `adb96b5f` says remaining bloat moved from supplement trimming into shared system layers, added a smaller autonomy prompt contract and compact soul profile, and explicitly directs next investigation toward `SessionStateJson` if autonomy still exceeds budget.
- Current `StardewAutonomyFirstCallContextBudgetPolicy` is a char-based first-call autonomy policy with a default 5000-char budget, dynamic recall cap, old tool-result pruning, and protected active task/current user/system/builtin memory/tail handling.
- `Agent.ChatAsync` applies the first-call context budget policy only on tool-loop iteration 1 when prepared context exists.
- `ContextManager.PrepareContextAsync` already has recent-window trimming, evicted-message summarization, state compaction under critical pressure, compact autonomy soul profile selection, and prompt packet logging for Stardew runtime sessions.
- Reference `external/hermes-agent-main` uses a model-aware `ContextEngine` / `ContextCompressor` with prompt-token tracking from API responses, threshold-based compression, tool-output pruning, boundary alignment for tool pairs, iterative summary updates, session hygiene, and compression-count / token telemetry.

Constraints:
- Do not create a second NPC task store or a shadow prompt architecture.
- Preserve single `PromptBuilder` / `ContextManager` / `SoulService` pipeline unless explicitly rejected.
- Cost reduction must not drop active todo, current user intent, latest tool-call/result group, or stable NPC identity/persona boundaries.
- Prefer measurable budget gates and runtime telemetry over prompt-only claims.
- No implementation inside deep-interview.

Unknowns/open questions:
- Should the first milestone target first-call prompt size only, or every LLM call inside an autonomy tool loop and private-chat multi-turn sessions?
- What per-NPC per-tick or per-minute cost budget should define success?
- Should compression be lossy summary-based, deterministic pruning/state projection first, or a hybrid?
- Is using an auxiliary cheap summarizer acceptable, or should the first pass avoid extra LLM calls entirely?
- Which content is allowed to be dropped, summarized, or referenced by handle instead of injected?

Decision-boundary unknowns:
- Whether OMX may change default autonomy cadence / max tool iterations / model routing to reduce cost.
- Whether OMX may add user-visible cost telemetry and hard-stop budget behavior.
- Whether lossy context summaries are acceptable for NPC memory/task continuity.

Likely codebase touchpoints:
- `src/Context/ContextManager.cs`
- `src/Context/PromptBuilder.cs`
- `src/Context/SessionState.cs`
- `src/Context/TokenBudget.cs`
- `src/Core/Agent.cs`
- `src/Core/FirstCallContextBudget.cs`
- `src/runtime/StardewAutonomyFirstCallContextBudgetPolicy.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyContextBudgetTests.cs`
- `external/hermes-agent-main/agent/context_compressor.py`
- `external/hermes-agent-main/agent/context_engine.py`
- `external/hermes-agent-main/website/docs/developer-guide/context-compression-and-caching.md`
