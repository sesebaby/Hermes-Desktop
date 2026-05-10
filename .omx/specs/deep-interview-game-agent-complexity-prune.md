# Deep Interview Spec: Game Agent Complexity Prune

## Metadata

- Profile: standard
- Rounds: 6
- Final ambiguity: 19%
- Threshold: 20%
- Context type: brownfield
- Context snapshot: `.omx/context/game-agent-complexity-prune-20260510T095534Z.md`
- Interview transcript: `.omx/interviews/game-agent-complexity-prune-20260510T095534Z.md`
- Source plan: `.omx/plans/Hermes-Desktop复杂度精简计划（详细审查版）.md`

## Intent

Clarify what should be removed, retained, or reshaped in Hermes-Desktop when the product is framed as a multi-game agent platform, with Stardew Valley as the first scenario.

## Desired Outcome

The project should present a clearer game-agent platform core. Cleanup should reduce product noise, dead code, misleading surfaces, and default-startup/UI clutter without reducing the reusable multi-game agent substrate.

## In Scope

- Item-by-item review of the existing detailed cleanup plan.
- Classifying candidates as keep, delete, reshape as platform module, or defer.
- Evaluating whether generic-looking modules can become cross-game substrate.
- Identifying modules that should be removed because they are neither game-relevant nor platform-relevant.
- Identifying modules that should be renamed, reconnected, or moved to optional platform layers.
- Considering documentation/archive/search-noise cleanup.

## Out of Scope / Non-goals

- Do not touch core NPC runtime.
- Do not restructure NPC memory, soul, transcript, todo, skills, MCP, cron, autonomy, private chat, bridge, or host execution boundaries.
- Do not implement code changes during the deep-interview phase.
- Do not delete modules solely because they are currently unwired if they have strong future fit as platform substrate.

## Decision Boundaries

- Clearly irrelevant or unwired candidates may be recommended for direct deletion.
- A candidate should not be recommended for deletion if it has strong future fit as world knowledge, game lore/rules, cross-game orchestration, background cognition, observability, tooling, or authoring substrate.
- Connected systems such as Dreamer/Insights require explicit product-fit discussion before deletion.
- First-pass discussion should optimize for platform-core clarity rather than maximum line-count reduction.

## Constraints

- Preserve all game-agent core substrate required by NPC runtime.
- Use source code and project files as evidence, not outdated plans or README claims.
- Treat `.omx/plans/Hermes-Desktop复杂度精简计划（详细审查版）.md` as a candidate list, not authority.
- Existing user worktree changes must not be reverted.

## Acceptance Criteria

- Each candidate receives one of: keep, delete, reshape as platform module, defer.
- Each candidate includes a reason.
- Each candidate includes a core NPC runtime risk check.
- Future-platform fit is considered separately from current wiring.
- Any "delete" conclusion requires weak current wiring and weak future platform fit.
- Any "reshape" conclusion says what game-semantic role the module should become.

## Brownfield Evidence Notes

- `Buddy` has Desktop UI, DI registration, resources, and tests; no direct `src/runtime` or `src/games/stardew` references were found in the initial grep pass.
- `Wiki` has data-layer code and DI registration; initial grep found no obvious UI/tool entry, but it may fit as future world knowledge/game lore/rules substrate.
- `CoordinatorService` and `AgentService` are DI-registered; initial grep found no direct game runtime references, but orchestration may be a future multi-game platform question.
- `AutoDreamService` exists and tests assert it is not the default Desktop startup path.
- `MixtureOfAgentsTool` exists but is not listed in `AgentCapabilityAssembler.BuiltInToolNames`.
- `Dreamer/Insights` are active Desktop-connected systems via `StartDreamerBackground`, `DreamerStatus`, `InsightsService`, and Dashboard references; they are not zero-dependency deletion candidates.

## Review Template

For each item:

```text
Decision: keep | delete | reshape as platform module | defer
Reason: <why this matches the multi-game agent platform boundary>
Core NPC runtime risk check: <none | low | medium | high, with reason>
```

## Suggested First Items

1. Buddy
2. Wiki
3. Coordinator / AgentService
4. AutoDreamService
5. MixtureOfAgentsTool
6. Dreamer / Insights
7. Reference/archive/documentation noise

## Item Decisions

### Buddy

Decision: delete

Reason: Buddy is a Desktop-facing companion/gacha/persona feature. The initial evidence pass found Desktop UI, DI registration, resources, and tests, but no direct NPC runtime or Stardew game-chain dependency. It does not clearly map to the retained multi-game agent platform substrate.

Core NPC runtime risk check: none. Deletion should avoid `src/runtime/**`, `src/games/stardew/**`, and the NPC memory/soul/transcript/todo/skill/MCP/cron path.

### Wiki

Decision: reshape as platform module

Reason: Although the initial evidence pass found weak current product wiring, the module maps well to a future multi-game platform role: world knowledge, game lore, location semantics, rule references, NPC-facing common knowledge, and authoring support. It should not remain a generic "wiki" platform leftover; it should be renamed/reframed around game-world knowledge.

Core NPC runtime risk check: low. Keep the first pass away from core NPC runtime; later integration should happen through explicit tool/surface design rather than implicit prompt injection.

### Coordinator / AgentService

Decision: keep

Reason: Current direct game-runtime wiring appears weak, but the capability area maps to multi-game platform needs: multi-NPC orchestration, long-running game tasks, isolated worker lanes, and cross-game coordination. Retention does not mean the current generic code-agent/worktree/remote/team shape is automatically product-correct; it should later be reviewed for game-semantic boundaries.

Core NPC runtime risk check: low. Current evidence suggests it is outside the NPC runtime main path. Any future integration must preserve NPC state/memory/tool boundaries and must not introduce a second NPC task or memory system.

### AutoDreamService

Decision: delete

Reason: This is the old `src/dream/` implementation and is not the current Desktop startup path. Existing tests explicitly assert it remains dormant by default, while the active background cognition path is `src/dreamer/`. Keeping both creates a misleading two-Dream-system architecture.

Core NPC runtime risk check: none. The deletion target is outside the core NPC runtime path and should not affect `src/runtime/**` or `src/games/stardew/**`.

### MixtureOfAgentsTool

Decision: defer

Reason: The current implementation is an unregistered generic "ask multiple models and synthesize" tool, so it is not currently part of the game agent core. However, the capability idea may later map to game semantics such as multi-NPC consensus, world planning, or an in-world council. Keep it temporarily, but do not treat the current generic tool shape as product-ready.

Core NPC runtime risk check: none. It is not currently registered in `AgentCapabilityAssembler.BuiltInToolNames` and should remain outside the core NPC runtime unless redesigned with explicit game semantics.

### Dreamer / Insights

Decision: reshape as platform module

Reason: These are active Desktop-connected systems, not dead code. Keep the background cognition and observability capability, but reshape it away from generic project reflection into game-world and NPC-runtime value: world event review, NPC behavior summaries, village state digests, abnormal behavior diagnostics, and long-running story/thread memory support.

Core NPC runtime risk check: medium. The system currently has background startup and Dashboard wiring, so any future change must verify Desktop startup, logging, cancellation, and observability behavior. It must not inject host-generated decisions into NPC prompts or bypass the existing NPC tool/execution boundary.

### Reference / Archive / Outdated Documentation Noise

Decision: reshape/archive

Reason: `external/`, `参考项目/`, `.omx/archives`, and outdated architecture documents are not runtime code, but they still carry reference value for a multi-game agent platform. The cleanup goal is to reduce search noise and prevent stale documents from being mistaken for current implementation facts, not to destroy useful reference material.

Core NPC runtime risk check: none. These are documentation/reference/archive actions and should not touch runtime code.

Recommended handling:

- `external/`: keep an index in the active repo; migrate large snapshots or raw reference code out of the main search path.
- `参考项目/`: preserve as reference material, but isolate from normal product-code search.
- `.omx/archives`: migrate or mark as historical archive; never treat as current architecture authority.
- Outdated docs: move to `docs/archive/` or an external archive and add an explicit stale-warning header.

### Localization Resources

Decision: keep

Reason: Keep Chinese and English resources only. This matches the current product need and avoids unnecessary churn in the WinUI resource system. Do not add third-language resource maintenance scope unless explicitly needed later.

Core NPC runtime risk check: none. Localization resources are Desktop UI concerns and do not affect NPC runtime.
