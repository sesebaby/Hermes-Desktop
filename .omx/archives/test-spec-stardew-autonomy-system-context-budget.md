# Test Spec: Stardew Autonomy System Context Budget Reduction

Date: 2026-05-06
Status: Approved for planning handoff by `$ralplan`
PRD: `.omx/plans/prd-stardew-autonomy-system-context-budget.md`

## Goal

Prove that Stardew autonomy first-call system context shrinks materially without regressing the behavior contracts that keep NPC autonomy correct and visible.

## Test Targets

- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs`
- autonomy prompt assembly path around:
  - `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
  - `src/runtime/NpcRuntimeContextFactory.cs`
  - `src/runtime/AgentCapabilityAssembler.cs`
  - `src/skills/SkillManager.cs`

Likely test files:

- `Desktop/HermesDesktop.Tests/Stardew/*`
- new or expanded prompt-budget tests where appropriate

## Required Coverage

### 1. Supplement compaction

Add a test that builds the autonomy supplement for Haley and Penny and asserts:

- the supplement no longer contains full required skill file bodies
- the supplement still contains the minimal autonomy contract sections needed for:
  - movement
  - task continuity
  - visible feedback
  - on-demand history lookup

### 2. Autonomy does not receive global skills-mandatory payload

Add a test that prepares autonomy first-call context and asserts the repo-wide global `Skills (mandatory)` block is absent from the autonomy system prompt path.

If a Stardew-only micro-index is retained, assert:

- it is Stardew-scoped only
- it stays under a fixed character cap
- it is admitted through the shared prompt assembly framework, not a second custom prompt stack

### 3. First-call system size regression

Add a budget-focused test for autonomy first-call context that asserts:

- system-layer char count is substantially below the current `22K-24K` shape
- the normal autonomy path can fit within the 5K-first-context objective when protected content is otherwise reasonable

This test must fail if full skill text or the full global mandatory skill index reappears.

### 4. Contract preservation

Add tests that prove the compact autonomy contract still encodes:

- `stardew_move` required for physical movement
- `stardew_task_status` required for long-action polling
- `todo` continuity for accepted commitments
- `session_search` escalation when history is missing
- `stardew_speak` visible feedback expectation
- blocked/failed task updates with short reasons

These should be structural/contract tests, not full-text snapshot tests.

### 5. Private chat isolation

Add or update a test proving the system still uses one prompt assembly framework and one shared source of truth, while runtime-mode admission rules decide whether the compact autonomy block set applies.

This test should guard against accidental dual-track drift:

- no separate shadow prompt builder with divergent source assets
- no duplicated summary or memory lane
- mode differences are expressed as block admission, not a second architecture

## Regression Expectations

- Existing active-task-context classification tests remain green.
- Existing dynamic recall trimming behavior remains green.
- Existing protected-tail / task-status continuity tests remain green.

## Verification Commands

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Stardew"
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

## Manual Runtime Verification

After implementation, inspect:

- `%LOCALAPPDATA%\hermes\hermes-cs\logs\hermes.log`
- latest runtime activity logs under:
  - `%LOCALAPPDATA%\hermes\hermes-cs\runtime\stardew\games\stardew-valley\saves\<saveId>\npc\<npcId>\profiles\<profileId>\activity\runtime.jsonl`

Expected log movement:

- `systemChars` materially lower than the current `22K-24K`
- `activeTaskChars` remains small
- `currentUserChars` remains non-zero
- `budgetUnmetReason` is no longer dominated by `core_system_over_budget` in normal first-call cases

## Non-Goals for Tests

- Do not snapshot entire prompt bodies unless the snapshot is intentionally small and contract-oriented.
- Do not test for prose wording beyond the minimal contract.
- Do not add tests that lock the old full-text supplement shape.
- Do not encode dual-track assumptions into test names or fixtures.
