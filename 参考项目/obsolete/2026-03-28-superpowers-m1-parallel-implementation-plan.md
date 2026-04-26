# Superpowers M1 Parallel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended when subagents are available, especially for parallel waves) or superpowers:executing-plans (fallback when staying sequential or when subagents are unavailable). Steps use checkbox (`- [ ]`) syntax for tracking.

> **UI hard gate:** Any task that creates or changes player-visible Launcher or Stardew surfaces must explicitly use `ui-ux-pro-max` during design, implementation, and review.

**Goal:** Turn the reviewed `superpowers` M1 design into an executable parallel delivery plan, with the Stardew visible mod route isolated from launcher work but explicitly bound to the real runtime surface once Task 4 lands, and the rest of the platform split into merge-safe tracks.

**Architecture:** Keep `M1-source-faithful` as the only implementation target. Build four code slices with hard boundaries: `Launcher + Supervisor`, `Local Runtime + Stardew Adapter`, `Cloud Control + Hosted Narrative Orchestration`, and `Stardew Mod visible surfaces`. The mod owns host hooks, visible surfaces, and local fact capture; runtime owns canonical contracts, deterministic validation, and local orchestration glue; cloud owns signed decisions plus the M1 hosted narrative mainline with canonical history/memory authority; launcher only consumes `launchReadinessVerdict` plus derived facts.

**Tech Stack:** `WPF + .NET 10` for Launcher, `ASP.NET Core + .NET 10` for Local Runtime and Cloud Control, `C# + SMAPI + Harmony + .NET 6` for Stardew Mod, `net6.0` for Stardew-mod tests and any shared contracts consumed by the mod, `SQLite` for local runtime cache, `xUnit` for tests.

**Execution Mode Recommendation:** Subagent-driven recommended

**Parallelization:** Parallel waves allowed

**Parallel Waves:** Wave 1: Task 3 + Task 4; Wave 2: Task 5

---

## Scope Split

This master design covers multiple independent subsystems, so this plan is intentionally split into four executable workstreams instead of pretending one engineer should touch everything in one thread:

1. `Launcher / Supervisor`
2. `Runtime / Stardew Adapter`
3. `Stardew Mod visible route`
4. `Cloud Control / Evidence`

If execution starts and any one workstream grows beyond this plan, write a child plan for that workstream before expanding scope.

## Hard Gates

- Always re-read these truth sources before changing code:
  - `docs/superpowers/governance/current-phase-boundary.md`
  - `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
  - `docs/superpowers/contracts/product/narrative-base-pack-contract.md`
  - `docs/superpowers/contracts/product/capability-claim-matrix.md`
  - `docs/superpowers/contracts/product/sku-entitlement-claim-matrix.md`
  - `docs/superpowers/contracts/runtime/deterministic-command-event-contract.md`
  - `docs/superpowers/contracts/runtime/narrative-degradation-contract.md`
  - `docs/superpowers/contracts/runtime/trace-audit-contract.md`
  - `docs/superpowers/governance/evidence-review-index.md`
  - `docs/superpowers/governance/client-exposure-threat-model.md`
  - `docs/superpowers/governance/waivers/narrative-base-pack-waiver-register.md`
  - `docs/superpowers/profiles/games/stardew-valley/game-integration-profile.md`
  - `docs/superpowers/specs/attachments/2026-03-27-superpowers-player-launcher-appendix.md`
  - `docs/superpowers/specs/attachments/2026-03-27-superpowers-client-runtime-topology-appendix.md`
  - `docs/superpowers/specs/attachments/2026-03-27-stardew-m1-appendix.md`
  - `docs/superpowers/specs/attachments/2026-03-27-stardew-first-wave-reference-mod-migration-appendix.md`
  - `docs/superpowers/specs/attachments/2026-03-27-stardew-capability-flow-appendix.md`
  - `docs/superpowers/specs/attachments/2026-03-27-stardew-context-summary-fields-appendix.md`
  - `docs/superpowers/specs/attachments/2026-03-27-stardew-hook-mapping-appendix.md`
  - `docs/superpowers/specs/attachments/2026-03-27-stardew-surface-commit-trace-failure-appendix.md`
- No code task is allowed to freeze DTOs, visibility, or readiness behavior before the authoritative runtime/governance docs are aligned for the same scope.
- `M1 core` must stay limited to `dialogue + memory + social transaction / commitment`.
- `group_chat` and `remote_direct_one_to_one` are `implementation_only`: implement, test, review, and index evidence, but do not treat them as current `M1` sellability or exit-criteria proof.
- `Launcher` must not invent a second readiness truth source. It only renders `launchReadinessVerdict` plus derived display facts.
- `Launcher Supervisor` may materialize or refresh the launcher-managed verdict artifact, but it must not become readiness-policy truth source or self-authorize launch policy.
- `access decision`, `cost attribution`, and `runtime state` must also have explicit single-owner and consumer contracts in the lower governance/runtime docs before any code task freezes those shapes.
- `Runtime` must not become a local AI truth source. It stays a thin coordinator, validator, projector, and trace glue layer.
- `Stardew Mod` must preserve semantic hooks even if final SMAPI or Harmony patch points change.
- `implementation_only` player-visible surfaces must stay default-hidden until exposure config, disclosure/evidence rows, and any required waiver state are in place.
- `Cloud Control + Hosted Narrative Orchestration` is part of the M1 mainline and must not be planned as a stub-only placeholder.
- No committed repo file may hardcode one contributor's Stardew install path. Resolve local game paths from a machine-local contract such as `SUPERPOWERS_STARDEW_GAME_PATH`, then derive `Mods\Superpowers.Stardew.Mod` and `StardewModdingAPI.exe` from that root at execution time.
- Every player-visible Launcher or Stardew surface must carry an explicit `ui-ux-pro-max` basis before implementation starts:
  - visual direction
  - accessibility
  - responsive / resize behavior
  - empty / failure / delayed / recovery surfaces

## Personal Focus Lane

Your personal route is the Stardew visible mod route. Treat these as your owned paths unless reassigned:

- Repo source root: `games/stardew-valley/Superpowers.Stardew.Mod/**`
- Repo test root: `tests/Superpowers.Stardew.Mod.Tests/**`
- Local deploy target: derive from `SUPERPOWERS_STARDEW_GAME_PATH` as `$(SUPERPOWERS_STARDEW_GAME_PATH)\Mods\Superpowers.Stardew.Mod\`
- Local baseline mods folder to inspect during testing: derive from `SUPERPOWERS_STARDEW_GAME_PATH` as `$(SUPERPOWERS_STARDEW_GAME_PATH)\Mods\`

Your main tasks in this plan are Task 2, Task 5, Task 7, and Task 8.

## Recovered Anchor Set

Every AI-behavior task that touches private dialogue, thought, memory, item/gift semantics, remote direct, or group chat must anchor review and implementation to:

- `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.ailm/ChatCompletions.cs`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod/AIServer.cs`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/jsonrepair/JsonRepair.cs`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod.data/PrivateMessageData.cs`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod.data/GroupMessageData.cs`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod.data/ContactGroupMessageData.cs`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod/ContactGroup.cs`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.mod/ExperienceData.cs`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decoded_prompts/角色卡模板.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decoded_prompts/群聊.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.ui.ext/Patch_UINPCUnitInfoItem.cs`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.ui.ext/Patch_UINPCUnitInfoItem_Init.cs`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld.ui.ext/UIIconPropItemExt.cs`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decoded_prompts/对话.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decoded_prompts/记忆压缩.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decoded_prompts/行为指令.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/decoded_prompts/交易.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/PRIVATE_CONTACT_AI_ARCHITECTURE_CN.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/LONG_TERM_MEMORY_ANALYSIS.md`
- `recovered_mod/GGBH_OpenAIWorld_20260326/分析文档/成熟MOD源码锚定_通用AI游戏方案提炼.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-first-wave-reference-mod-migration-appendix.md`
- `docs/superpowers/specs/attachments/2026-03-27-stardew-capability-flow-appendix.md`

Required review lens for those tasks:
- Trigger
- Snapshot
- Summary Builder
- Intent / `actions[]` schema
- Parser / Repair / Normalizer
- Projector / Executor

## Target File Structure

### Solution And Shared Build

- Create: `src/Superpowers.sln`
- Create: `Directory.Build.props`
- Create: `scripts/dev/new-worktrees.ps1`
- Create: `scripts/dev/publish-launcher.ps1`
- Create: `scripts/dev/publish-runtime-local.ps1`
- Create: `scripts/dev/publish-cloud-control.ps1`
- Create: `scripts/dev/publish-stardew-mod.ps1`
- Create: `scripts/dev/sync-stardew-mod.ps1`
- Create: `scripts/dev/run-runtime-local.ps1`
- Create: `scripts/dev/run-cloud-control.ps1`
- Create: `scripts/dev/check-http-health.ps1`
- Create: `scripts/dev/run-stardew-smapi.ps1`
- Create: `scripts/dev/verify-superpowers-governance-evidence.ps1`

### Launcher / Supervisor

- Create: `src/Superpowers.Launcher/Superpowers.Launcher.csproj`
- Create: `src/Superpowers.Launcher/App.xaml`
- Create: `src/Superpowers.Launcher/App.xaml.cs`
- Create: `src/Superpowers.Launcher/MainWindow.xaml`
- Create: `src/Superpowers.Launcher/MainWindow.xaml.cs`
- Create: `src/Superpowers.Launcher/Views/HomeView.xaml`
- Create: `src/Superpowers.Launcher/Views/GameLibraryView.xaml`
- Create: `src/Superpowers.Launcher/Views/ProductRedeemView.xaml`
- Create: `src/Superpowers.Launcher/Views/NotificationsView.xaml`
- Create: `src/Superpowers.Launcher/Views/StardewGameConfigView.xaml`
- Create: `src/Superpowers.Launcher/Views/SupportView.xaml`
- Create: `src/Superpowers.Launcher/Views/SettingsView.xaml`
- Create: `src/Superpowers.Launcher/ViewModels/HomeViewModel.cs`
- Create: `src/Superpowers.Launcher/ViewModels/ProductRedeemViewModel.cs`
- Create: `src/Superpowers.Launcher/ViewModels/NotificationsViewModel.cs`
- Create: `src/Superpowers.Launcher/ViewModels/StardewGameConfigViewModel.cs`
- Create: `src/Superpowers.Launcher/ViewModels/SupportViewModel.cs`
- Create: `src/Superpowers.Launcher/ViewModels/SettingsViewModel.cs`
- Create: `src/Superpowers.Launcher.Supervisor/Superpowers.Launcher.Supervisor.csproj`
- Create: `src/Superpowers.Launcher.Supervisor/Readiness/LaunchReadinessVerdict.cs`
- Create: `src/Superpowers.Launcher.Supervisor/Readiness/LaunchReadinessPolicySnapshot.cs`
- Create: `src/Superpowers.Launcher.Supervisor/Readiness/CapabilityAccessDecision.cs`
- Create: `src/Superpowers.Launcher.Supervisor/Readiness/StardewReadinessEvaluator.cs`
- Create: `src/Superpowers.Launcher.Supervisor/State/RuntimeHealthFact.cs`
- Create: `src/Superpowers.Launcher.Supervisor/State/RuntimePreflightFact.cs`
- Create: `src/Superpowers.Launcher.Supervisor/State/RuntimePreflightRef.cs`
- Create: `src/Superpowers.Launcher.Supervisor/State/RecoveryEntryRef.cs`

### Runtime / Adapter / Cloud

- Create: `src/Superpowers.Runtime.Contracts/Superpowers.Runtime.Contracts.csproj`
- Create: `src/Superpowers.Runtime.Contracts/Responses/CommittedOutcomeEnvelope.cs`
- Create: `src/Superpowers.Runtime.Local/Superpowers.Runtime.Local.csproj`
- Create: `src/Superpowers.Runtime.Local/Program.cs`
- Create: `src/Superpowers.Runtime.Local/Endpoints/PrivateDialogueEndpoint.cs`
- Create: `src/Superpowers.Runtime.Local/Endpoints/ThoughtEndpoint.cs`
- Create: `src/Superpowers.Runtime.Local/Endpoints/ItemGiftEndpoint.cs`
- Create: `src/Superpowers.Runtime.Local/Endpoints/RemoteDirectEndpoint.cs`
- Create: `src/Superpowers.Runtime.Local/Endpoints/GroupChatEndpoint.cs`
- Create: `src/Superpowers.Runtime.Local/Normalization/ActionRepairNormalizer.cs`
- Create: `src/Superpowers.Runtime.Local/Projection/MirroredWritebackProjector.cs`
- Create: `src/Superpowers.Runtime.Local/Determinism/DeterministicValidator.cs`
- Create: `src/Superpowers.Runtime.Local/History/LocalProjectionStore.cs`
- Create: `src/Superpowers.Runtime.Local/Memory/LocalSummaryCache.cs`
- Create: `src/Superpowers.Runtime.Stardew/Superpowers.Runtime.Stardew.csproj`
- Create: `src/Superpowers.Runtime.Stardew/Contracts/HostSummaryEnvelope.cs`
- Create: `src/Superpowers.Runtime.Stardew/Contracts/PrivateDialogueRequest.cs`
- Create: `src/Superpowers.Runtime.Stardew/Contracts/RemoteDirectRequest.cs`
- Create: `src/Superpowers.Runtime.Stardew/Contracts/GroupChatTurnRequest.cs`
- Create: `src/Superpowers.Runtime.Stardew/Contracts/ThoughtRequest.cs`
- Create: `src/Superpowers.Runtime.Stardew/Adapter/StardewSnapshotBuilder.cs`
- Create: `src/Superpowers.Runtime.Stardew/Adapter/StardewRelationSnapshotBuilder.cs`
- Create: `src/Superpowers.Runtime.Stardew/Adapter/StardewHostSummaryBuilder.cs`
- Create: `src/Superpowers.Runtime.Stardew/Adapter/StardewHistoryJoinKeyFactory.cs`
- Create: `src/Superpowers.CloudControl/Superpowers.CloudControl.csproj`
- Create: `src/Superpowers.CloudControl/Program.cs`
- Create: `src/Superpowers.CloudControl/Launch/LaunchDecisionController.cs`
- Create: `src/Superpowers.CloudControl/Support/SupportTicketController.cs`
- Create: `src/Superpowers.CloudControl/Narrative/HostedNarrativeController.cs`
- Create: `src/Superpowers.CloudControl/Narrative/HostedNarrativeOrchestrator.cs`
- Create: `src/Superpowers.CloudControl/History/CanonicalHistoryStore.cs`
- Create: `src/Superpowers.CloudControl/Memory/CanonicalMemoryStore.cs`

### Stardew Mod

- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Superpowers.Stardew.Mod.csproj`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/manifest.json`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/ModEntry.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Config/ModConfig.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/NpcInteractionHooks.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/MenuLifecycleHooks.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/WorldLifecycleHooks.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/ItemCarrierHooks.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Runtime/RuntimeClient.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Exposure/BuildExposureConfig.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/AiDialogueMenu.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/NpcInfoPanelMenu.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/MemoryTabView.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/GroupHistoryTabView.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/RelationTabView.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/ItemTabView.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/ThoughtTabView.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/PhoneDirectMessageMenu.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/OnsiteGroupChatOverlay.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/PhoneActiveGroupChatMenu.cs`

### Tests

- Create: `tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj`
- Create: `tests/Superpowers.Launcher.Tests/Readiness/StardewReadinessEvaluatorTests.cs`
- Create: `tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj`
- Create: `tests/Superpowers.Runtime.Tests/Contracts/RuntimeContractShapeTests.cs`
- Create: `tests/Superpowers.Runtime.Tests/Dialogue/PrivateDialogueEndpointTests.cs`
- Create: `tests/Superpowers.Runtime.Tests/Thought/ThoughtEndpointTests.cs`
- Create: `tests/Superpowers.Runtime.Tests/Items/ItemGiftEndpointTests.cs`
- Create: `tests/Superpowers.Runtime.Tests/Remote/RemoteDirectEndpointTests.cs`
- Create: `tests/Superpowers.Runtime.Tests/Group/GroupChatEndpointTests.cs`
- Create: `tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj`
- Create: `tests/Superpowers.Stardew.Mod.Tests/UI/AiDialogueMenuTests.cs`
- Create: `tests/Superpowers.Stardew.Mod.Tests/UI/NpcInfoPanelMenuTests.cs`
- Create: `tests/Superpowers.Stardew.Mod.Tests/UI/GroupHistoryTabViewTests.cs`
- Create: `tests/Superpowers.Stardew.Mod.Tests/UI/PhoneDirectMessageMenuTests.cs`
- Create: `tests/Superpowers.Stardew.Mod.Tests/UI/OnsiteGroupChatOverlayTests.cs`
- Create: `tests/Superpowers.Stardew.Mod.Tests/UI/PhoneActiveGroupChatMenuTests.cs`
- Create: `tests/Superpowers.Stardew.Mod.Tests/Hooks/HookSemanticEventTests.cs`
- Create: `tests/Superpowers.Stardew.Mod.Tests/Hooks/ItemCarrierHooksTests.cs`

## Parallelization Decision

- `Sequential only` for Task 1 and Task 2. They freeze the repo shape, the machine-local path contract, and the canonical contract surface.
- `Wave 1` is safe after Task 2 because write ownership is disjoint:
  - Task 3 owns `src/Superpowers.Launcher/**`, `src/Superpowers.Launcher.Supervisor/**`, `tests/Superpowers.Launcher.Tests/**`
  - Task 4 owns `src/Superpowers.Runtime.Contracts/**`, `src/Superpowers.Runtime.Local/**`, `src/Superpowers.Runtime.Stardew/**`, `src/Superpowers.CloudControl/**`, `tests/Superpowers.Runtime.Tests/**`
  - Task 5 is excluded from Wave 1 because its `RuntimeClient` and deploy loop must consume the real Task 4 runtime routes and response envelopes, not speculative mod-local mirrors.
- Merge checkpoint after Wave 1: build launcher/runtime projects, run launcher/runtime test suites, and publish runtime artifacts that Task 5 will consume.
- `Wave 2` starts only after Task 4 is committed. Task 5 branches from that commit so the mod-side `RuntimeClient` can bind to the real runtime endpoint names, failure envelope semantics, and hosted narrative responses already frozen by Task 2 and implemented by Task 4.
- Merge checkpoint after Wave 2: run the Stardew-mod suite plus the shared solution build before Task 6 integration.
- `Sequential only` after Task 6 because `private dialogue`, `remote direct`, and `group chat` share actor-owned history identities, mirrored projections, disclosure config, and the same local mod deployment target.
- Final integration for Task 9 and Task 10 is `Sequential only` because they touch shared hook routing, evidence, and readiness wiring.

## Workspace Isolation

- Task 3 workspace: `..\AllGameInAI-worktrees\superpowers-wave1-launcher`
- Task 4 workspace: `..\AllGameInAI-worktrees\superpowers-wave1-runtime`
- Task 5 workspace: `..\AllGameInAI-worktrees\superpowers-wave2-stardew-visual`

Create them with:

```powershell
git worktree add ..\AllGameInAI-worktrees\superpowers-wave1-launcher -b feat/wave1-launcher <TASK2_COMMIT_SHA>
git worktree add ..\AllGameInAI-worktrees\superpowers-wave1-runtime -b feat/wave1-runtime <TASK2_COMMIT_SHA>
git worktree add ..\AllGameInAI-worktrees\superpowers-wave2-stardew-visual -b feat/wave2-stardew-visual <TASK4_COMMIT_SHA>
```

Only create Wave 1 worktrees after Task 2 is committed, and always branch them from the Task 2 commit. Create the Task 5 worktree only after Task 4 is committed, and branch it from the Task 4 commit so the mod work consumes actual runtime code instead of duplicating endpoint assumptions.

## Task 1: Scaffold The Solution And Developer Loop

**Files:**
- Create: `src/Superpowers.sln`
- Create: `Directory.Build.props`
- Create: `src/Superpowers.Launcher/Superpowers.Launcher.csproj`
- Create: `src/Superpowers.Launcher.Supervisor/Superpowers.Launcher.Supervisor.csproj`
- Create: `src/Superpowers.Runtime.Contracts/Superpowers.Runtime.Contracts.csproj`
- Create: `src/Superpowers.Runtime.Local/Superpowers.Runtime.Local.csproj`
- Create: `src/Superpowers.Runtime.Stardew/Superpowers.Runtime.Stardew.csproj`
- Create: `src/Superpowers.CloudControl/Superpowers.CloudControl.csproj`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Superpowers.Stardew.Mod.csproj`
- Create: `tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj`
- Create: `tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj`
- Create: `tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj`
- Create: `scripts/dev/new-worktrees.ps1`
- Create: `scripts/dev/publish-launcher.ps1`
- Create: `scripts/dev/publish-runtime-local.ps1`
- Create: `scripts/dev/publish-cloud-control.ps1`
- Create: `scripts/dev/publish-stardew-mod.ps1`
- Create: `scripts/dev/sync-stardew-mod.ps1`
- Create: `scripts/dev/run-runtime-local.ps1`
- Create: `scripts/dev/run-cloud-control.ps1`
- Create: `scripts/dev/check-http-health.ps1`
- Create: `scripts/dev/run-stardew-smapi.ps1`
- Create: `scripts/dev/verify-superpowers-governance-evidence.ps1`

**Wave:** Sequential

**Dependencies:** None

**Parallel-safe:** No

**Agent ownership:** `src/Superpowers.sln`, `Directory.Build.props`, `src/**/*.csproj`, `games/stardew-valley/Superpowers.Stardew.Mod/Superpowers.Stardew.Mod.csproj`, `games/stardew-valley/Superpowers.Stardew.Mod/manifest.json`, `tests/**/*.csproj`, `scripts/dev/new-worktrees.ps1`, `scripts/dev/publish-launcher.ps1`, `scripts/dev/publish-runtime-local.ps1`, `scripts/dev/publish-cloud-control.ps1`, `scripts/dev/publish-stardew-mod.ps1`, `scripts/dev/sync-stardew-mod.ps1`, `scripts/dev/run-runtime-local.ps1`, `scripts/dev/run-cloud-control.ps1`, `scripts/dev/check-http-health.ps1`, `scripts/dev/run-stardew-smapi.ps1`, `scripts/dev/verify-superpowers-governance-evidence.ps1`

**Merge checkpoint:** `dotnet sln src/Superpowers.sln list`

**Workspace isolation:** Sequential task in main worktree

- [x] **Step 1: Create the empty solution and script files**

```powershell
dotnet new sln -n Superpowers -o src
New-Item -ItemType Directory -Force -Path .\src\Superpowers.Launcher, .\src\Superpowers.Launcher.Supervisor, .\src\Superpowers.Runtime.Contracts, .\src\Superpowers.Runtime.Local, .\src\Superpowers.Runtime.Stardew, .\src\Superpowers.CloudControl, .\games\stardew-valley\Superpowers.Stardew.Mod, .\tests\Superpowers.Launcher.Tests, .\tests\Superpowers.Runtime.Tests, .\tests\Superpowers.Stardew.Mod.Tests, .\scripts\dev
dotnet new wpf -n Superpowers.Launcher -o .\src\Superpowers.Launcher --framework net10.0
dotnet new classlib -n Superpowers.Launcher.Supervisor -o .\src\Superpowers.Launcher.Supervisor --framework net10.0
dotnet new classlib -n Superpowers.Runtime.Contracts -o .\src\Superpowers.Runtime.Contracts --framework net6.0
dotnet new web -n Superpowers.Runtime.Local -o .\src\Superpowers.Runtime.Local --framework net10.0
dotnet new classlib -n Superpowers.Runtime.Stardew -o .\src\Superpowers.Runtime.Stardew --framework net6.0
dotnet new webapi -n Superpowers.CloudControl -o .\src\Superpowers.CloudControl --framework net10.0
dotnet new classlib -n Superpowers.Stardew.Mod -o .\games\stardew-valley\Superpowers.Stardew.Mod --framework net6.0
dotnet new xunit -n Superpowers.Launcher.Tests -o .\tests\Superpowers.Launcher.Tests --framework net10.0
dotnet new xunit -n Superpowers.Runtime.Tests -o .\tests\Superpowers.Runtime.Tests --framework net10.0
dotnet new xunit -n Superpowers.Stardew.Mod.Tests -o .\tests\Superpowers.Stardew.Mod.Tests --framework net6.0
New-Item -ItemType File -Force -Path .\Directory.Build.props
Set-Content -Path .\games\stardew-valley\Superpowers.Stardew.Mod\manifest.json -Value '{\"Name\":\"Superpowers.Stardew.Mod\",\"Author\":\"AllGameInAI\",\"Version\":\"0.1.0\",\"Description\":\"Superpowers Stardew mod scaffold\",\"UniqueID\":\"AllGameInAI.Superpowers.Stardew.Mod\",\"EntryDll\":\"Superpowers.Stardew.Mod.dll\",\"MinimumApiVersion\":\"4.0.0\"}'
New-Item -ItemType File -Path .\scripts\dev\new-worktrees.ps1
New-Item -ItemType File -Path .\scripts\dev\publish-launcher.ps1
New-Item -ItemType File -Path .\scripts\dev\publish-runtime-local.ps1
New-Item -ItemType File -Path .\scripts\dev\publish-cloud-control.ps1
New-Item -ItemType File -Path .\scripts\dev\publish-stardew-mod.ps1
New-Item -ItemType File -Path .\scripts\dev\sync-stardew-mod.ps1
New-Item -ItemType File -Path .\scripts\dev\run-runtime-local.ps1
New-Item -ItemType File -Path .\scripts\dev\run-cloud-control.ps1
New-Item -ItemType File -Path .\scripts\dev\check-http-health.ps1
New-Item -ItemType File -Path .\scripts\dev\run-stardew-smapi.ps1
New-Item -ItemType File -Path .\scripts\dev\verify-superpowers-governance-evidence.ps1
```

- [x] **Step 2: Add all project shells to the solution**

```powershell
dotnet sln .\src\Superpowers.sln add .\src\Superpowers.Launcher\Superpowers.Launcher.csproj .\src\Superpowers.Launcher.Supervisor\Superpowers.Launcher.Supervisor.csproj .\src\Superpowers.Runtime.Contracts\Superpowers.Runtime.Contracts.csproj .\src\Superpowers.Runtime.Local\Superpowers.Runtime.Local.csproj .\src\Superpowers.Runtime.Stardew\Superpowers.Runtime.Stardew.csproj .\src\Superpowers.CloudControl\Superpowers.CloudControl.csproj .\games\stardew-valley\Superpowers.Stardew.Mod\Superpowers.Stardew.Mod.csproj .\tests\Superpowers.Launcher.Tests\Superpowers.Launcher.Tests.csproj .\tests\Superpowers.Runtime.Tests\Superpowers.Runtime.Tests.csproj .\tests\Superpowers.Stardew.Mod.Tests\Superpowers.Stardew.Mod.Tests.csproj
```

- [x] **Step 3: Write the minimal SMAPI/Harmony mod artifact contract**

```powershell
# publish-stardew-mod.ps1 contract
param(
  [string]$ProjectPath = '.\games\stardew-valley\Superpowers.Stardew.Mod\Superpowers.Stardew.Mod.csproj',
  [string]$OutputDir = '.\artifacts\stardew-mod\Superpowers.Stardew.Mod'
)
# must run dotnet publish, copy manifest.json, and leave a runnable SMAPI mod artifact directory

# publish-launcher.ps1 contract
# must publish launcher to .\artifacts\launcher

# publish-runtime-local.ps1 contract
# must publish local runtime to .\artifacts\runtime-local

# publish-cloud-control.ps1 contract
# must publish cloud control to .\artifacts\cloud-control
```

Required SMAPI build contract in this step:
- `games/stardew-valley/Superpowers.Stardew.Mod/Superpowers.Stardew.Mod.csproj` must target `net6.0`
- it must carry `Pathoschild.Stardew.ModBuildConfig`
- it must enable Harmony support
- it must resolve game assemblies from `$(GamePath)` or equivalent local game path contract
- publish output must contain `Superpowers.Stardew.Mod.dll` and `manifest.json`

Required concrete edit in this step:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <EnableHarmony>true</EnableHarmony>
    <GamePath Condition="'$(GamePath)' == ''">$(SUPERPOWERS_STARDEW_GAME_PATH)</GamePath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Pathoschild.Stardew.ModBuildConfig" Version="4.1.0" PrivateAssets="All" />
  </ItemGroup>
</Project>
```

- [x] **Step 3: Wire the external Stardew mod deployment path into the sync script**

```powershell
$modsPath = Join-Path $env:SUPERPOWERS_STARDEW_GAME_PATH 'Mods\Superpowers.Stardew.Mod'
```

- [x] **Step 4: Implement the script contract before treating the scripts as gates**

```powershell
# sync-stardew-mod.ps1 contract
param(
  [switch]$DryRun,
  [switch]$RequireManifest,
  [string]$SourceDir,
  [string]$TargetDir
)
# must resolve TargetDir from `SUPERPOWERS_STARDEW_GAME_PATH` when omitted, validate source exists, optionally validate manifest when -RequireManifest is set, show copy plan in DryRun, and mirror-copy from published artifact output in normal mode

# run-stardew-smapi.ps1 contract
param(
  [switch]$ValidateOnly,
  [string]$SmapiPath,
  [string]$LogPath,
  [int]$TimeoutSec = 120,
  [switch]$KillOnTimeout
)
# must resolve SmapiPath from `SUPERPOWERS_STARDEW_GAME_PATH` when omitted, validate path/log arguments in ValidateOnly mode, or launch SMAPI, wait up to TimeoutSec for `Superpowers.Stardew.Mod` load marker in the log, and kill/fail on timeout when -KillOnTimeout is set

# run-runtime-local.ps1 / run-cloud-control.ps1 contract
# must start the published artifact in the background and write a PID file under .\artifacts\pids\
# runtime-local health port is fixed to `127.0.0.1:5051`
# cloud-control health port is fixed to `127.0.0.1:7061`

# check-http-health.ps1 contract
# must query a required /healthz endpoint with timeout and fail on non-200 result

# verify-superpowers-governance-evidence.ps1 contract
param(
  [string]$CandidateRevision,
  [string]$EvidenceRoot = '.\docs\superpowers\governance',
  [string]$ArtifactRoot = '.\artifacts\release-evidence'
)
# must fail if CandidateRevision does not equal `git rev-parse HEAD`, if freshness windows are expired, if linked review/evidence files are missing, if `traceIds` / `recoveryEvidenceRef` do not resolve into exported release-evidence artifacts, or if Evidence Review Index links are missing
```

- [x] **Step 5: Run the solution listing command**

Run: `dotnet sln src/Superpowers.sln list`  
Expected: PASS with the scaffolded project list and no path errors.

- [x] **Step 6: Run the sync script in dry-run mode**

Run: `$env:SUPERPOWERS_STARDEW_GAME_PATH='<LOCAL_STARDEW_GAME_PATH>'; powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-stardew-mod.ps1 -ProjectPath .\games\stardew-valley\Superpowers.Stardew.Mod\Superpowers.Stardew.Mod.csproj -OutputDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod && powershell -ExecutionPolicy Bypass -File .\scripts\dev\sync-stardew-mod.ps1 -DryRun -SourceDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod`
Expected: PASS with a published artifact directory, explicit source/target listing, and no filesystem writes while targeting the machine-local `Mods\Superpowers.Stardew.Mod` directory derived from `SUPERPOWERS_STARDEW_GAME_PATH`.

- [x] **Step 7: Run the SMAPI wrapper in validation-only mode**

Run: `$env:SUPERPOWERS_STARDEW_GAME_PATH='<LOCAL_STARDEW_GAME_PATH>'; powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-stardew-smapi.ps1 -ValidateOnly -LogPath .\artifacts\logs\smapi-latest.log -TimeoutSec 120 -KillOnTimeout && Test-Path .\scripts\dev\publish-launcher.ps1 && Test-Path .\scripts\dev\publish-runtime-local.ps1 && Test-Path .\scripts\dev\publish-cloud-control.ps1 && Test-Path .\scripts\dev\run-runtime-local.ps1 && Test-Path .\scripts\dev\run-cloud-control.ps1 && Test-Path .\scripts\dev\check-http-health.ps1 && Test-Path .\scripts\dev\verify-superpowers-governance-evidence.ps1`
Expected: PASS with argument validation and no launch.

- [x] **Step 8: Commit**

```powershell
git add src/Superpowers.sln Directory.Build.props src/Superpowers.Launcher src/Superpowers.Launcher.Supervisor src/Superpowers.Runtime.Contracts src/Superpowers.Runtime.Local src/Superpowers.Runtime.Stardew src/Superpowers.CloudControl games/stardew-valley/Superpowers.Stardew.Mod tests/Superpowers.Launcher.Tests tests/Superpowers.Runtime.Tests tests/Superpowers.Stardew.Mod.Tests scripts/dev/new-worktrees.ps1 scripts/dev/publish-launcher.ps1 scripts/dev/publish-runtime-local.ps1 scripts/dev/publish-cloud-control.ps1 scripts/dev/publish-stardew-mod.ps1 scripts/dev/sync-stardew-mod.ps1 scripts/dev/run-runtime-local.ps1 scripts/dev/run-cloud-control.ps1 scripts/dev/check-http-health.ps1 scripts/dev/run-stardew-smapi.ps1 scripts/dev/verify-superpowers-governance-evidence.ps1
git commit -m "chore: scaffold superpowers solution and stardew dev loop"
```

## Task 2: Align Authoritative Contracts Before Freezing Code Shapes

**Files:**
- Modify: `docs/superpowers/contracts/product/capability-claim-matrix.md`
- Modify: `docs/superpowers/contracts/product/sku-entitlement-claim-matrix.md`
- Modify: `docs/superpowers/contracts/runtime/deterministic-command-event-contract.md`
- Modify: `docs/superpowers/contracts/runtime/narrative-degradation-contract.md`
- Modify: `docs/superpowers/contracts/runtime/trace-audit-contract.md`
- Modify: `docs/superpowers/governance/evidence-review-index.md`
- Modify: `docs/superpowers/governance/client-exposure-threat-model.md`
- Modify: `docs/superpowers/governance/waivers/narrative-base-pack-waiver-register.md`
- Modify: `docs/superpowers/specs/attachments/2026-03-27-superpowers-player-launcher-appendix.md`
- Modify: `docs/superpowers/specs/attachments/2026-03-27-stardew-m1-appendix.md`
- Create: `src/Superpowers.Runtime.Contracts/Superpowers.Runtime.Contracts.csproj`
- Create: `src/Superpowers.Runtime.Contracts/Responses/CommittedOutcomeEnvelope.cs`
- Create: `src/Superpowers.Launcher.Supervisor/Superpowers.Launcher.Supervisor.csproj`
- Create: `src/Superpowers.Launcher.Supervisor/Readiness/LaunchReadinessVerdict.cs`
- Create: `src/Superpowers.Launcher.Supervisor/Readiness/LaunchReadinessPolicySnapshot.cs`
- Create: `src/Superpowers.Launcher.Supervisor/Readiness/CapabilityAccessDecision.cs`
- Create: `src/Superpowers.Launcher.Supervisor/Readiness/StardewReadinessEvaluator.cs`
- Create: `src/Superpowers.Launcher.Supervisor/State/RuntimeHealthFact.cs`
- Create: `src/Superpowers.Launcher.Supervisor/State/RuntimePreflightFact.cs`
- Create: `src/Superpowers.Launcher.Supervisor/State/RuntimePreflightRef.cs`
- Create: `src/Superpowers.Launcher.Supervisor/State/RecoveryEntryRef.cs`
- Create: `src/Superpowers.Runtime.Stardew/Contracts/HostSummaryEnvelope.cs`
- Create: `src/Superpowers.Runtime.Stardew/Contracts/PrivateDialogueRequest.cs`
- Create: `src/Superpowers.Runtime.Stardew/Contracts/RemoteDirectRequest.cs`
- Create: `src/Superpowers.Runtime.Stardew/Contracts/GroupChatTurnRequest.cs`
- Create: `src/Superpowers.Runtime.Stardew/Contracts/ThoughtRequest.cs`
- Test: `tests/Superpowers.Runtime.Tests/Contracts/RuntimeContractShapeTests.cs`
- Test: `tests/Superpowers.Launcher.Tests/Readiness/StardewReadinessEvaluatorTests.cs`

**Wave:** Sequential

**Dependencies:** Task 1

**Parallel-safe:** No

**Agent ownership:** `docs/superpowers/contracts/product/**`, `docs/superpowers/contracts/runtime/**`, `docs/superpowers/governance/evidence-review-index.md`, `docs/superpowers/governance/client-exposure-threat-model.md`, `docs/superpowers/governance/waivers/narrative-base-pack-waiver-register.md`, `docs/superpowers/specs/attachments/2026-03-27-superpowers-player-launcher-appendix.md`, `docs/superpowers/specs/attachments/2026-03-27-stardew-m1-appendix.md`, `src/Superpowers.Runtime.Contracts/**`, `src/Superpowers.Runtime.Stardew/Contracts/**`, `src/Superpowers.Launcher.Supervisor/**`, `tests/Superpowers.Runtime.Tests/Contracts/**`, `tests/Superpowers.Launcher.Tests/Readiness/**`

**Merge checkpoint:** `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~RuntimeContractShapeTests" && dotnet test tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj --filter "FullyQualifiedName~StardewReadinessEvaluatorTests" && rg -n "readiness policy|launchReadinessVerdict|runtimeHealthFact|failureClass|recoveryEntryRef|implementation_only|access decision|cost attribution|runtime state|groupHistoryDisclosureState|open_for_player|not_open_for_player" docs/superpowers/contracts docs/superpowers/governance`

**Workspace isolation:** Sequential task in main worktree

- [x] **Step 1: Update authoritative product/runtime/governance docs for current M1 scope before code freeze**

```powershell
rg -n "readiness policy|launchReadinessVerdict|implementation_only|remote_direct_one_to_one|group_chat|access decision|cost attribution|runtime state|groupHistoryDisclosureState|open_for_player|not_open_for_player" .\docs\superpowers\contracts .\docs\superpowers\governance
```

Required doc freeze in this step:
- `groupHistoryDisclosureState` values must be frozen as exactly `open_for_player` and `not_open_for_player`
- `groupHistoryDisclosureState` ownership must be frozen to current build/title exposure config and reused as the single disclosure truth across UI, Runtime, and Mod
- `readiness policy` owner and consumer path must be frozen explicitly, not inferred from `launchReadinessVerdict`
- `launchReadinessVerdict` and `runtimeHealthFact` minimum fields and join inputs must be frozen before Wave 1
- `launchReadinessPolicySnapshot`, `capabilityAccessDecision`, `runtimePreflightFact`, and `runtimePreflightRef` must be frozen as deterministic verdict inputs before Wave 1
- `access decision`, `cost attribution`, and `runtime state` must each name one authoritative owner and consumer path
- Launcher UI basis must be frozen in the player launcher appendix before Task 3 starts
- Stardew visible-surface UI basis must be frozen in the Stardew M1 appendix before Task 5 starts
- `remoteDirectRequest.threadKey`, `groupChatTurnRequest.participantSetRef`, `groupChatTurnRequest.inputSequenceId`, `thoughtRequest.surfaceId`, and `summarySelectionHint` must be frozen in the authoritative Mod -> Runtime contract
- `hostSummaryEnvelope` must freeze `summaryEnvelopeId`, `snapshotCapturedAt`, and the eight required summary buckets
- ref-vs-inline wire-shape split is forbidden: both sides must agree on the same envelope/reference shape
- per-surface `failureClass` mappings (`availability_blocked`, `render_failed`, `submission_failed`, `refresh_failed`) and shared `recoveryEntryRef` consumption must be frozen before Wave 1
- support-path `failureClass` mappings must also freeze `diagnostic_export_failed` and `diagnostic_redaction_failed` before Wave 1
- runtime/cloud must freeze one canonical fail-closed failure envelope with stable `reason_code`, deterministic status mapping, and explicit propagation into `CommittedOutcomeEnvelope`, runtime endpoints, and cloud controllers before Wave 1

- [x] **Step 2: Write failing tests for the four request DTOs and readiness-consumer rules**

```csharp
[Fact]
public void PrivateDialogueRequest_MustExposeHostSummaryRef() { }

[Fact]
public void LauncherSupervisor_MustNotInventSecondReadinessTruth() { }

[Fact]
public void AccessDecisionAndCostAttribution_MustRemainExternalTruths() { }

[Fact]
public void ReadinessPolicyOwnership_MustRemainInLowerContracts_NotLauncherUi() { }

[Fact]
public void ModRuntimeContract_MustFreezeThreadKeySurfaceIdSummarySelectionHint_AndNoRefInlineSplit() { }

[Fact]
public void FailureClassAndRecoveryEntry_MustBeFrozenPerSurfaceBeforeWave1() { }

[Fact]
public void HostSummaryEnvelope_MustFreezeSummaryEnvelopeIdSnapshotCapturedAt_AndEightSummaryBuckets() { }

[Fact]
public void CanonicalFailureEnvelope_MustFreezeReasonCodeAndDeterministicStatusMapping_BeforeWave1() { }

[Fact]
public void ReadinessInputs_MustFreezePolicySnapshotAccessDecisionAndPreflightFacts_BeforeWave1() { }
```

- [x] **Step 3: Run tests to verify they fail on missing types**

Run: `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~RuntimeContractShapeTests"`  
Expected: FAIL with missing contract types.

- [x] **Step 4: Implement the minimal contract and readiness records**

```csharp
public sealed record PrivateDialogueRequest(
    string RequestId,
    string GameId,
    string ActorId,
    string TargetId,
    string TriggerKind,
    string HostDialogueRecordRef,
    string SceneSnapshotRef,
    string RelationSnapshotRef,
    string RecentPrivateHistoryRef,
    string HostSummaryRef);
```

- [x] **Step 5: Re-run the contract and readiness tests plus the doc-shape grep**

Run: `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~RuntimeContractShapeTests" && dotnet test tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj --filter "FullyQualifiedName~StardewReadinessEvaluatorTests" && rg -n "readiness policy|launchReadinessVerdict|runtimeHealthFact|failureClass|recoveryEntryRef|implementation_only|access decision|cost attribution|runtime state|groupHistoryDisclosureState|open_for_player|not_open_for_player" docs/superpowers/contracts docs/superpowers/governance`  
Expected: PASS.

- [x] **Step 6: Commit**

```powershell
git add docs/superpowers/contracts/product/capability-claim-matrix.md docs/superpowers/contracts/product/sku-entitlement-claim-matrix.md docs/superpowers/contracts/runtime/deterministic-command-event-contract.md docs/superpowers/contracts/runtime/narrative-degradation-contract.md docs/superpowers/contracts/runtime/trace-audit-contract.md docs/superpowers/governance/evidence-review-index.md docs/superpowers/governance/client-exposure-threat-model.md docs/superpowers/governance/waivers/narrative-base-pack-waiver-register.md docs/superpowers/specs/attachments/2026-03-27-superpowers-player-launcher-appendix.md docs/superpowers/specs/attachments/2026-03-27-stardew-m1-appendix.md src/Superpowers.Runtime.Contracts/Superpowers.Runtime.Contracts.csproj src/Superpowers.Runtime.Contracts/Responses/CommittedOutcomeEnvelope.cs src/Superpowers.Runtime.Stardew/Contracts/HostSummaryEnvelope.cs src/Superpowers.Runtime.Stardew/Contracts/PrivateDialogueRequest.cs src/Superpowers.Runtime.Stardew/Contracts/RemoteDirectRequest.cs src/Superpowers.Runtime.Stardew/Contracts/GroupChatTurnRequest.cs src/Superpowers.Runtime.Stardew/Contracts/ThoughtRequest.cs src/Superpowers.Launcher.Supervisor/Superpowers.Launcher.Supervisor.csproj src/Superpowers.Launcher.Supervisor/Readiness/LaunchReadinessVerdict.cs src/Superpowers.Launcher.Supervisor/Readiness/LaunchReadinessPolicySnapshot.cs src/Superpowers.Launcher.Supervisor/Readiness/CapabilityAccessDecision.cs src/Superpowers.Launcher.Supervisor/Readiness/StardewReadinessEvaluator.cs src/Superpowers.Launcher.Supervisor/State/RuntimeHealthFact.cs src/Superpowers.Launcher.Supervisor/State/RuntimePreflightFact.cs src/Superpowers.Launcher.Supervisor/State/RuntimePreflightRef.cs src/Superpowers.Launcher.Supervisor/State/RecoveryEntryRef.cs tests/Superpowers.Runtime.Tests/Contracts/RuntimeContractShapeTests.cs tests/Superpowers.Launcher.Tests/Readiness/StardewReadinessEvaluatorTests.cs
git commit -m "feat: align m1 governance contracts before code freeze"
```

## Task 3: Build Launcher Shell And Stardew Config Surface

**Required skill:** `ui-ux-pro-max`

**Files:**
- Create: `src/Superpowers.Launcher/Superpowers.Launcher.csproj`
- Create: `src/Superpowers.Launcher/App.xaml`
- Create: `src/Superpowers.Launcher/App.xaml.cs`
- Create: `src/Superpowers.Launcher/MainWindow.xaml`
- Create: `src/Superpowers.Launcher/MainWindow.xaml.cs`
- Create: `src/Superpowers.Launcher/Views/HomeView.xaml`
- Create: `src/Superpowers.Launcher/Views/GameLibraryView.xaml`
- Create: `src/Superpowers.Launcher/Views/ProductRedeemView.xaml`
- Create: `src/Superpowers.Launcher/Views/NotificationsView.xaml`
- Create: `src/Superpowers.Launcher/Views/StardewGameConfigView.xaml`
- Create: `src/Superpowers.Launcher/Views/SupportView.xaml`
- Create: `src/Superpowers.Launcher/Views/SettingsView.xaml`
- Create: `src/Superpowers.Launcher/ViewModels/HomeViewModel.cs`
- Create: `src/Superpowers.Launcher/ViewModels/ProductRedeemViewModel.cs`
- Create: `src/Superpowers.Launcher/ViewModels/NotificationsViewModel.cs`
- Create: `src/Superpowers.Launcher/ViewModels/StardewGameConfigViewModel.cs`
- Create: `src/Superpowers.Launcher/ViewModels/SupportViewModel.cs`
- Create: `src/Superpowers.Launcher/ViewModels/SettingsViewModel.cs`
- Test: `tests/Superpowers.Launcher.Tests/Readiness/StardewReadinessEvaluatorTests.cs`

**Wave:** Wave 1

**Dependencies:** Task 2

**Parallel-safe:** Yes

**Agent ownership:** `src/Superpowers.Launcher/**`, `tests/Superpowers.Launcher.Tests/**`

**Merge checkpoint:** `dotnet test tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj`

**Workspace isolation:** Isolated worktree

- [x] **Step 1: Write failing launcher tests for one-card Stardew config rendering, six-surface IA, support submission state, and single-source CTA binding**

```csharp
[Fact]
public void StardewCard_MustBindPrimaryCtaToLaunchReadinessVerdict() { }

[Fact]
public void LauncherMainNavigation_MustExposeHomeGamesProductRedeemNotificationsSupportAndSettings() { }

[Fact]
public void SupportView_MustHandleSubmittingReceiptAndDiagnosticFailureStates() { }
```

- [x] **Step 2: Run launcher tests to verify failure**

Run: `dotnet test tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj`  
Expected: FAIL with missing views or bindings.

- [x] **Step 3: Implement the launcher shell with `首页 / 游戏 / 产品与兑换 / 通知 / 支持与帮助 / 设置` plus the Stardew config page**

```xml
<Button Content="{Binding PrimaryActionLabel}" />
```

- [x] **Step 4: Re-run launcher tests**

Run: `dotnet test tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj`  
Expected: PASS.

- [x] **Step 5: Commit**

```powershell
git add src/Superpowers.Launcher tests/Superpowers.Launcher.Tests
git commit -m "feat: add launcher shell and stardew config surface"
```

## Task 4: Build Local Runtime, Adapter, And Hosted Narrative Mainline

**Required skill:** `ui-ux-pro-max` only for any player-visible artifacts touched during runtime/mainline verification; AI review in this task must explicitly use the Recovered Anchor Set and the 6-layer mapping.

**Files:**
- Create: `src/Superpowers.Runtime.Local/Superpowers.Runtime.Local.csproj`
- Create: `src/Superpowers.Runtime.Local/Program.cs`
- Create: `src/Superpowers.Runtime.Local/Endpoints/PrivateDialogueEndpoint.cs`
- Create: `src/Superpowers.Runtime.Local/Endpoints/ThoughtEndpoint.cs`
- Create: `src/Superpowers.Runtime.Local/Endpoints/ItemGiftEndpoint.cs`
- Create: `src/Superpowers.Runtime.Local/Endpoints/RemoteDirectEndpoint.cs`
- Create: `src/Superpowers.Runtime.Local/Endpoints/GroupChatEndpoint.cs`
- Create: `src/Superpowers.Runtime.Local/Normalization/ActionRepairNormalizer.cs`
- Create: `src/Superpowers.Runtime.Local/Projection/MirroredWritebackProjector.cs`
- Create: `src/Superpowers.Runtime.Local/Determinism/DeterministicValidator.cs`
- Create: `src/Superpowers.Runtime.Local/History/LocalProjectionStore.cs`
- Create: `src/Superpowers.Runtime.Local/Memory/LocalSummaryCache.cs`
- Create: `src/Superpowers.Runtime.Stardew/Superpowers.Runtime.Stardew.csproj`
- Create: `src/Superpowers.Runtime.Stardew/Adapter/StardewSnapshotBuilder.cs`
- Create: `src/Superpowers.Runtime.Stardew/Adapter/StardewRelationSnapshotBuilder.cs`
- Create: `src/Superpowers.Runtime.Stardew/Adapter/StardewHostSummaryBuilder.cs`
- Create: `src/Superpowers.Runtime.Stardew/Adapter/StardewHistoryJoinKeyFactory.cs`
- Create: `src/Superpowers.CloudControl/Superpowers.CloudControl.csproj`
- Create: `src/Superpowers.CloudControl/Program.cs`
- Create: `src/Superpowers.CloudControl/Launch/LaunchDecisionController.cs`
- Create: `src/Superpowers.CloudControl/Support/SupportTicketController.cs`
- Create: `src/Superpowers.CloudControl/Narrative/HostedNarrativeController.cs`
- Create: `src/Superpowers.CloudControl/Narrative/HostedNarrativeOrchestrator.cs`
- Create: `src/Superpowers.CloudControl/History/CanonicalHistoryStore.cs`
- Create: `src/Superpowers.CloudControl/Memory/CanonicalMemoryStore.cs`
- Create: `src/Superpowers.CloudControl/Projection/PendingVisibleStore.cs`
- Create: `src/Superpowers.CloudControl/Replay/CanonicalReplayEnvelopeStore.cs`
- Test: `tests/Superpowers.Runtime.Tests/Contracts/RuntimeContractShapeTests.cs`
- Test: `tests/Superpowers.Runtime.Tests/Dialogue/PrivateDialogueEndpointTests.cs`
- Test: `tests/Superpowers.Runtime.Tests/Narrative/HostedNarrativePathTests.cs`

**Wave:** Wave 1

**Dependencies:** Task 2

**Parallel-safe:** Yes

**Agent ownership:** `src/Superpowers.Runtime.Local/**`, `src/Superpowers.Runtime.Stardew/**`, `src/Superpowers.CloudControl/**`, `tests/Superpowers.Runtime.Tests/**`

**Merge checkpoint:** `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj`

**Workspace isolation:** Isolated worktree

- [x] **Step 1: Write failing runtime tests for DTO binding, localhost endpoint routing, Stardew host summary normalization, source-faithful `actions[]` retention, blocked/deferred outcomes, canonical replay envelope, mirrored writeback, `pending_visible` finalization, and hosted narrative authority/recovery**

Task-scoped AI gate in this step:
- re-read the Recovered Anchor Set
- document the 6-layer mapping for this task:
  - Trigger
  - Snapshot
  - Summary Builder
  - Intent / `actions[]` schema
  - Parser / Repair / Normalizer
  - Projector / Executor

```csharp
[Fact]
public async Task PrivateDialogueEndpoint_MustAcceptPrivateDialogueRequest() { }
```

- [x] **Step 2: Run runtime tests to verify failure**

Run: `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj`  
Expected: FAIL with missing endpoints and adapters.

- [x] **Step 3: Implement minimal localhost API, parse/repair/normalize flow, deterministic validator shell, canonical replay envelope write, mirrored writeback, `pending_visible` handling, and the M1 hosted narrative mainline with canonical history/memory authority**

```csharp
app.MapPost("/runtime/stardew/private-dialogue", PrivateDialogueEndpoint.HandleAsync);
```

- [x] **Step 4: Re-run runtime tests**

Run: `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj`  
Expected: PASS.

- [x] **Step 5: Commit**

```powershell
git add src/Superpowers.Runtime.Local src/Superpowers.Runtime.Stardew src/Superpowers.CloudControl tests/Superpowers.Runtime.Tests
git commit -m "feat: add local runtime adapter and hosted narrative mainline"
```

## Task 5: Build Stardew Visible Surface Shell And Local Deployment Loop

**Required skill:** `ui-ux-pro-max`

**Files:**
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Superpowers.Stardew.Mod.csproj`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/manifest.json`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/ModEntry.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Config/ModConfig.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Exposure/BuildExposureConfig.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/NpcInteractionHooks.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/MenuLifecycleHooks.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/WorldLifecycleHooks.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/ItemCarrierHooks.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/Runtime/RuntimeClient.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/AiDialogueMenu.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/NpcInfoPanelMenu.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/MemoryTabView.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/GroupHistoryTabView.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/RelationTabView.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/ItemTabView.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/ThoughtTabView.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Carriers/MailItemTextCarrier.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Carriers/RewardItemTextCarrier.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Carriers/TooltipItemTextCarrier.cs`
- Test: `tests/Superpowers.Stardew.Mod.Tests/UI/AiDialogueMenuTests.cs`
- Test: `tests/Superpowers.Stardew.Mod.Tests/UI/NpcInfoPanelMenuTests.cs`
- Test: `tests/Superpowers.Stardew.Mod.Tests/UI/GroupHistoryTabViewTests.cs`
- Test: `tests/Superpowers.Stardew.Mod.Tests/UI/ItemCarrierSurfaceTests.cs`
- Test: `tests/Superpowers.Stardew.Mod.Tests/Runtime/RuntimeClientTests.cs`
- Test: `tests/Superpowers.Stardew.Mod.Tests/Hooks/HookSemanticEventTests.cs`

**Wave:** Wave 2

**Dependencies:** Task 4

**Parallel-safe:** Yes

**Agent ownership:** `games/stardew-valley/Superpowers.Stardew.Mod/**`, `tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj`, `tests/Superpowers.Stardew.Mod.Tests/UI/**`, `tests/Superpowers.Stardew.Mod.Tests/Runtime/**`, `tests/Superpowers.Stardew.Mod.Tests/Hooks/**`, `scripts/dev/sync-stardew-mod.ps1`, `scripts/dev/run-stardew-smapi.ps1`

**Merge checkpoint:** `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~PrivateDialogueEndpointTests|FullyQualifiedName~HostedNarrativePathTests|FullyQualifiedName~RemoteDirectEndpointTests|FullyQualifiedName~GroupChatEndpointTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj && $env:SUPERPOWERS_STARDEW_GAME_PATH='<LOCAL_STARDEW_GAME_PATH>'; powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-stardew-mod.ps1 -ProjectPath .\games\stardew-valley\Superpowers.Stardew.Mod\Superpowers.Stardew.Mod.csproj -OutputDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod && powershell -ExecutionPolicy Bypass -File .\scripts\dev\sync-stardew-mod.ps1 -RequireManifest -DryRun -SourceDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod && powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-stardew-smapi.ps1 -LogPath .\artifacts\logs\smapi-latest.log -TimeoutSec 120 -KillOnTimeout`

**Workspace isolation:** Isolated worktree

Task 5 must branch from the committed Task 4 runtime baseline. `RuntimeClient` is not allowed to invent route names or response envelope shapes locally; any client-side round-trip expectation in this task must be verified against Task 4 runtime code or shared runtime tests.

- [x] **Step 1: Write failing mod tests for the frozen semantic-hook set, disclosure hooks, and runtime-client binding against the Task 4 route/envelope surface**

```csharp
[Theory]
[InlineData("hostDialogueRenderedAt")]
[InlineData("hostDialogueRecordedAt")]
[InlineData("hostDialogueExhaustedAt")]
[InlineData("aiDialogueOpenedAt")]
[InlineData("aiDialogueRenderedAt")]
[InlineData("aiDialogueClosedAt")]
[InlineData("infoPanelRenderedAt")]
[InlineData("thoughtRequestedAt")]
[InlineData("thoughtRenderedAt")]
[InlineData("itemEventRecordedAt")]
[InlineData("itemCarrierRenderedAt")]
[InlineData("groupHistoryDisclosureResolvedAt")]
[InlineData("remoteThreadOpenedAt")]
[InlineData("remoteSubmitQueuedAt")]
[InlineData("remoteMessageRenderedAt")]
[InlineData("groupParticipantSetFrozenAt")]
[InlineData("groupPlayerInputQueuedAt")]
[InlineData("groupTurnRenderedAt")]
[InlineData("memoryTabRenderedAt")]
[InlineData("groupHistoryTabRenderedAt")]
[InlineData("relationTabRenderedAt")]
[InlineData("itemTabRenderedAt")]
public void HookLayer_MustEmitRequiredSemanticHooks(string hookName) { }
```

- [x] **Step 2: Run mod tests to verify failure against the Task 4 baseline**

Run: `dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj`  
Expected: FAIL with missing mod types, hooks, or runtime-client compatibility gaps.

- [x] **Step 3: Implement the shell mod, visible menus, runtime client, build exposure config, and semantic hook emitters**

```csharp
public void EmitSemanticHook(string hookName, string traceId) { }
```

UI basis freeze in this step:
- Launcher: player-first, no backend terms, keyboard focusable primary CTA, explicit running/issue/recovery states, resize-safe game card
- Stardew surfaces: original-style overlay direction, readable labels, keyboard/controller focus, explicit empty/failure/loading/recovery states
- Runtime-client rule in this step: consume Task 4 endpoint names, failure envelope semantics, and response shapes directly; do not create a second mod-local transport contract that can drift before Task 6.

- [x] **Step 4: Sync the built mod into the real Stardew mods folder with `implementation_only` surfaces default-hidden**

Run: `$env:SUPERPOWERS_STARDEW_GAME_PATH='<LOCAL_STARDEW_GAME_PATH>'; powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-stardew-mod.ps1 -ProjectPath .\games\stardew-valley\Superpowers.Stardew.Mod\Superpowers.Stardew.Mod.csproj -OutputDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod && powershell -ExecutionPolicy Bypass -File .\scripts\dev\sync-stardew-mod.ps1 -RequireManifest -SourceDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod`
Expected: PASS and files copied to the machine-local `Mods\Superpowers.Stardew.Mod\` directory derived from `SUPERPOWERS_STARDEW_GAME_PATH`.

- [x] **Step 5: Re-run the mod tests and prove the published mod loads under the real SMAPI host**

Run: `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~PrivateDialogueEndpointTests|FullyQualifiedName~HostedNarrativePathTests|FullyQualifiedName~RemoteDirectEndpointTests|FullyQualifiedName~GroupChatEndpointTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj && $env:SUPERPOWERS_STARDEW_GAME_PATH='<LOCAL_STARDEW_GAME_PATH>'; powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-stardew-mod.ps1 -ProjectPath .\games\stardew-valley\Superpowers.Stardew.Mod\Superpowers.Stardew.Mod.csproj -OutputDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod && powershell -ExecutionPolicy Bypass -File .\scripts\dev\sync-stardew-mod.ps1 -RequireManifest -SourceDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod && powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-stardew-smapi.ps1 -LogPath .\artifacts\logs\smapi-latest.log -TimeoutSec 120 -KillOnTimeout`
Expected: PASS.

- [x] **Step 6: Commit**

```powershell
git add games/stardew-valley/Superpowers.Stardew.Mod tests/Superpowers.Stardew.Mod.Tests scripts/dev
git commit -m "feat: add stardew visible surface shell and deploy loop"
```

## Task 6: Merge Wave 1 And Verify Shared Build

**Files:**
- Modify: `src/Superpowers.sln`
- Modify: `src/Superpowers.Launcher/Superpowers.Launcher.csproj`
- Modify: `src/Superpowers.Launcher.Supervisor/Superpowers.Launcher.Supervisor.csproj`
- Modify: `src/Superpowers.Runtime.Contracts/Superpowers.Runtime.Contracts.csproj`
- Modify: `src/Superpowers.Runtime.Local/Superpowers.Runtime.Local.csproj`
- Modify: `src/Superpowers.Runtime.Stardew/Superpowers.Runtime.Stardew.csproj`
- Modify: `src/Superpowers.CloudControl/Superpowers.CloudControl.csproj`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/Superpowers.Stardew.Mod.csproj`
- Modify: `tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj`
- Modify: `tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj`
- Modify: `tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj`

**Wave:** Sequential

**Dependencies:** Task 3, Task 4, Task 5

**Parallel-safe:** No

**Agent ownership:** `src/Superpowers.sln`, `src/**/*.csproj`, `games/stardew-valley/Superpowers.Stardew.Mod/Superpowers.Stardew.Mod.csproj`, `tests/**/*.csproj`

**Merge checkpoint:** `dotnet build src/Superpowers.sln && dotnet test tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj`

**Workspace isolation:** Sequential task in main worktree

- [x] **Step 1: Merge Wave 1 in this exact order using pinned SHAs**

```powershell
git checkout <MAIN_WORKTREE_BRANCH>
git cherry-pick <TASK3_COMMIT_SHA>
git cherry-pick <TASK4_COMMIT_SHA>
git cherry-pick <TASK5_COMMIT_SHA>
git rev-parse HEAD
```
- [x] **Step 2: Fix only project references, package references, and solution entries touched by the merge**
- [x] **Step 3: Run the shared build and all current test suites**

Run: `dotnet build src/Superpowers.sln && dotnet test tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj`  
Expected: PASS.

- [x] **Step 4: Hand-check that `sync-stardew-mod.ps1` still deploys to the same external folder**

Run: `$env:SUPERPOWERS_STARDEW_GAME_PATH='<LOCAL_STARDEW_GAME_PATH>'; powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-stardew-mod.ps1 -ProjectPath .\games\stardew-valley\Superpowers.Stardew.Mod\Superpowers.Stardew.Mod.csproj -OutputDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod && powershell -ExecutionPolicy Bypass -File .\scripts\dev\sync-stardew-mod.ps1 -RequireManifest -DryRun -SourceDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod`
Expected: PASS with unchanged derived target path under the machine-local game root.

- [x] **Step 5: Commit**

```powershell
git add src/Superpowers.sln src/Superpowers.Launcher/Superpowers.Launcher.csproj src/Superpowers.Launcher.Supervisor/Superpowers.Launcher.Supervisor.csproj src/Superpowers.Runtime.Contracts/Superpowers.Runtime.Contracts.csproj src/Superpowers.Runtime.Local/Superpowers.Runtime.Local.csproj src/Superpowers.Runtime.Stardew/Superpowers.Runtime.Stardew.csproj src/Superpowers.CloudControl/Superpowers.CloudControl.csproj games/stardew-valley/Superpowers.Stardew.Mod/Superpowers.Stardew.Mod.csproj tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj
git commit -m "chore: merge wave1 launcher runtime and stardew shell"
```

## Task 7: Implement M1 Core Stardew Route End To End

**Required skill:** `ui-ux-pro-max`

**Files:**
- Modify: `src/Superpowers.Runtime.Local/Endpoints/PrivateDialogueEndpoint.cs`
- Modify: `src/Superpowers.Runtime.Local/Endpoints/ThoughtEndpoint.cs`
- Modify: `src/Superpowers.Runtime.Local/Endpoints/ItemGiftEndpoint.cs`
- Modify: `src/Superpowers.Runtime.Local/History/LocalProjectionStore.cs`
- Modify: `src/Superpowers.Runtime.Local/Memory/LocalSummaryCache.cs`
- Modify: `src/Superpowers.Runtime.Stardew/Adapter/StardewSnapshotBuilder.cs`
- Modify: `src/Superpowers.Runtime.Stardew/Adapter/StardewRelationSnapshotBuilder.cs`
- Modify: `src/Superpowers.Runtime.Stardew/Adapter/StardewHostSummaryBuilder.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/Exposure/BuildExposureConfig.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/NpcInteractionHooks.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/ItemCarrierHooks.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/UI/AiDialogueMenu.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/UI/NpcInfoPanelMenu.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/MemoryTabView.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/GroupHistoryTabView.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/RelationTabView.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/ItemTabView.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Tabs/ThoughtTabView.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Carriers/MailItemTextCarrier.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Carriers/RewardItemTextCarrier.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/UI/Carriers/TooltipItemTextCarrier.cs`
- Test: `tests/Superpowers.Runtime.Tests/Dialogue/PrivateDialogueEndpointTests.cs`
- Test: `tests/Superpowers.Runtime.Tests/Thought/ThoughtEndpointTests.cs`
- Test: `tests/Superpowers.Runtime.Tests/Items/ItemGiftEndpointTests.cs`
- Test: `tests/Superpowers.Stardew.Mod.Tests/UI/AiDialogueMenuTests.cs`
- Test: `tests/Superpowers.Stardew.Mod.Tests/UI/NpcInfoPanelMenuTests.cs`
- Test: `tests/Superpowers.Stardew.Mod.Tests/UI/GroupHistoryTabViewTests.cs`
- Test: `tests/Superpowers.Stardew.Mod.Tests/UI/ItemCarrierSurfaceTests.cs`
- Test: `tests/Superpowers.Stardew.Mod.Tests/Hooks/ItemCarrierHooksTests.cs`

**Wave:** Sequential

**Dependencies:** Task 6

**Parallel-safe:** No

**Agent ownership:** dialogue, thought, info-panel, group-history disclosure, item-carrier, and build-exposure files only

**Merge checkpoint:** `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~PrivateDialogueEndpointTests" && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~ThoughtEndpointTests" && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~ItemGiftEndpointTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~AiDialogueMenuTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~NpcInfoPanelMenuTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~GroupHistoryTabViewTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~ItemCarrierSurfaceTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~ItemCarrierHooksTests"`

**Workspace isolation:** Sequential task in main worktree

- [x] **Step 1: Write failing tests for host-derived private-history normalization, stale thought cancellation, distinct item-carrier semantics, exact `groupHistoryDisclosureState` semantics, and per-surface `failureClass` / `recoveryEntryRef` mappings**

Task-scoped AI gate in this step:
- re-read the Recovered Anchor Set
- document the 6-layer mapping for the private-dialogue / thought / item route before implementation

```csharp
[Fact]
public void ThoughtSwitchNpc_MustMarkPreviousResultStale() { }

[Fact]
public void GroupHistoryDisclosureState_MustUseOpenForPlayerOrNotOpenForPlayer_AndPreserveReplayVsEmptyVsNotOpen() { }

[Fact]
public void ItemCarriers_MustKeepMailRewardAndTooltipAsDistinctCommittedSurfaces() { }

[Fact]
public void SurfaceFailureClassMappings_MustStayFrozenAcrossRuntimeModAndLauncher() { }

[Fact]
public void ItemCommit_MustRequireCarrierRenderedAuthoritativeRecordAndDeliveryOutcome() { }

[Fact]
public void ItemContext_MustUseItemModDataAndStableItemRefJoin() { }
```

- [x] **Step 2: Run the task test set to verify failure**

Run: `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~PrivateDialogueEndpointTests" && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~ThoughtEndpointTests" && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~ItemGiftEndpointTests"`  
Expected: FAIL.

- [x] **Step 3: Implement the end-to-end M1 core route**

```csharp
if (result.IsStale) return ThoughtCommitResult.Stale();
```

Source-faithful freeze in this step:
- retain recovered `actions[]` semantics through parse/repair/normalize
- produce explicit blocked/deferred deterministic outcomes for non-approved actions
- write canonical replay envelope and mirrored writeback before declaring replay-eligible success
- feed accepted outcomes into later memory compression
- item/gift committed semantics must require `carrier rendered` + `authoritative item-event record` + `actual delivery/no-delivery/rejected outcome`
- default item context store must remain `item.modData`
- authoritative `itemRef` join must follow the frozen runtime rule

- [x] **Step 4: Re-run runtime and mod tests for the route**

Run: `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~PrivateDialogueEndpointTests" && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~ThoughtEndpointTests" && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~ItemGiftEndpointTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~AiDialogueMenuTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~NpcInfoPanelMenuTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~GroupHistoryTabViewTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~ItemCarrierSurfaceTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~ItemCarrierHooksTests"`  
Expected: PASS.

- [x] **Step 5: Deploy and hand-check the visible route**

Run: `powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-stardew-mod.ps1 -ProjectPath .\games\stardew-valley\Superpowers.Stardew.Mod\Superpowers.Stardew.Mod.csproj -OutputDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod && powershell -ExecutionPolicy Bypass -File .\scripts\dev\sync-stardew-mod.ps1 -RequireManifest -SourceDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod -TargetDir 'D:\Stardew Valley\Stardew Valley.v1.6.15\Mods\Superpowers.Stardew.Mod'`  
Expected: PASS and the mod lands in `D:\Stardew Valley\Stardew Valley.v1.6.15\Mods\Superpowers.Stardew.Mod\`.

- [x] **Step 6: Commit**

```powershell
git add src/Superpowers.Runtime.Local src/Superpowers.Runtime.Stardew games/stardew-valley/Superpowers.Stardew.Mod tests/Superpowers.Runtime.Tests tests/Superpowers.Stardew.Mod.Tests
git commit -m "feat: implement stardew m1 core visible route"
```

## Task 8: Implement Implementation-Only Channels

**Required skill:** `ui-ux-pro-max`

**Files:**
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/Exposure/BuildExposureConfig.cs`
- Modify: `src/Superpowers.Runtime.Local/Endpoints/RemoteDirectEndpoint.cs`
- Modify: `src/Superpowers.Runtime.Local/Endpoints/GroupChatEndpoint.cs`
- Modify: `src/Superpowers.Runtime.Local/History/LocalProjectionStore.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/MenuLifecycleHooks.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/Hooks/WorldLifecycleHooks.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/PhoneDirectMessageMenu.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/OnsiteGroupChatOverlay.cs`
- Create: `games/stardew-valley/Superpowers.Stardew.Mod/UI/PhoneActiveGroupChatMenu.cs`
- Test: `tests/Superpowers.Runtime.Tests/Remote/RemoteDirectEndpointTests.cs`
- Test: `tests/Superpowers.Runtime.Tests/Group/GroupChatEndpointTests.cs`
- Test: `tests/Superpowers.Stardew.Mod.Tests/UI/PhoneDirectMessageMenuTests.cs`
- Test: `tests/Superpowers.Stardew.Mod.Tests/UI/OnsiteGroupChatOverlayTests.cs`
- Test: `tests/Superpowers.Stardew.Mod.Tests/UI/PhoneActiveGroupChatMenuTests.cs`

**Wave:** Sequential

**Dependencies:** Task 7

**Parallel-safe:** No

**Agent ownership:** implementation-only channel files, shared history projection file, and implementation-only exposure config only

**Merge checkpoint:** `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~RemoteDirectEndpointTests" && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~GroupChatEndpointTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~PhoneDirectMessageMenuTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~OnsiteGroupChatOverlayTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~PhoneActiveGroupChatMenuTests"`

**Workspace isolation:** Sequential task in main worktree

- [x] **Step 1: Write failing tests for `available_now/unavailable_now`, `contactGroupId` reuse, unread state, participant freeze, default-hidden implementation-only exposure, shared actor-owned history joins, mirrored participant projections, and recovered `ContactGroup` bucket/sidecar semantics**

Task-scoped AI gate in this step:
- re-read the Recovered Anchor Set
- document the 6-layer mapping for remote-direct / group-chat before implementation

```csharp
[Fact]
public void RemoteDirect_WhenUnavailable_MustNotCreatePendingVisibleTurn() { }
```

- [x] **Step 2: Run the task test set to verify failure**

Run: `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~RemoteDirectEndpointTests" && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~GroupChatEndpointTests"`  
Expected: FAIL.

- [x] **Step 3: Implement remote-direct and split group-chat channels without changing current readiness semantics**

```csharp
return availabilityState == "unavailable_now"
    ? RemoteDirectResult.UnavailableNow()
    : RemoteDirectResult.AvailableNow();
```

Required behavioral freeze in this step:
- accepted remote-direct turns must write into the shared actor-owned direct/private history truth, not a second channel-local history source
- delivered group turns must mirror into each participant's private-history projection
- `ContactGroup` persistence must keep `contactGroupId`, message bucket, `unreadCount`, `doNotDisturb`, and raw source-style sidecar or equivalent
- recovered non-player remote group activity must continue to append background thread updates and unread increments even while player-visible implementation-only surfaces stay hidden
- authoritative cross-channel join key must stay `historyOwnerActorId + canonicalRecordId`
- `messageIndex` must not become the cross-channel dedupe / replay / audit key
- every projection record must carry `historyOwnerActorId`, `canonicalRecordId`, `sourceChannelType`, and `projectionKind`
- all channel behavior must be reviewed against the Recovered Anchor Set before completion
- `PhoneDirectMessageMenu`, `OnsiteGroupChatOverlay`, and `PhoneActiveGroupChatMenu` must each carry explicit visual direction, accessibility, resize behavior, and empty/failure/delayed/recovery states before implementation is considered complete

UI basis freeze for implementation-only surfaces:
- `PhoneDirectMessageMenu`: phone-thread visual framing, keyboard focusable send/retry path, explicit unavailable/loading/failure states
- `OnsiteGroupChatOverlay`: onsite bubble/input framing, readable speaker separation, no silent-drop failure state
- `PhoneActiveGroupChatMenu`: remote thread framing, unread/DND visibility, explicit not-open/loading/failure/recovery states

- [x] **Step 4: Re-run runtime and mod channel tests**

Run: `dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~RemoteDirectEndpointTests" && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~GroupChatEndpointTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~PhoneDirectMessageMenuTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~OnsiteGroupChatOverlayTests" && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj --filter "FullyQualifiedName~PhoneActiveGroupChatMenuTests"`  
Expected: PASS.

- [x] **Step 5: Sync the mod with `implementation_only` surfaces disabled by default and verify they stay hidden in the default player build; local exposure override must remain blocked until Task 10 evidence/waiver closure**

Run: `powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-stardew-mod.ps1 -ProjectPath .\games\stardew-valley\Superpowers.Stardew.Mod\Superpowers.Stardew.Mod.csproj -OutputDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod && powershell -ExecutionPolicy Bypass -File .\scripts\dev\sync-stardew-mod.ps1 -RequireManifest -SourceDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod -TargetDir 'D:\Stardew Valley\Stardew Valley.v1.6.15\Mods\Superpowers.Stardew.Mod'`  
Expected: PASS and the default player build keeps `手机私信`, `现场群聊`, and `手机主动群聊` hidden; local exposure override remains blocked before Task 10 governance closure.

- [x] **Step 6: Commit**

```powershell
git add src/Superpowers.Runtime.Local games/stardew-valley/Superpowers.Stardew.Mod tests/Superpowers.Runtime.Tests tests/Superpowers.Stardew.Mod.Tests
git commit -m "feat: implement stardew implementation-only channels"
```

## Task 9: Reconcile Sequential Channel Work And Wire Readiness / Recovery End To End

**Required skill:** `ui-ux-pro-max`

**Files:**
- Modify: `src/Superpowers.Launcher/ViewModels/StardewGameConfigViewModel.cs`
- Modify: `src/Superpowers.Launcher/ViewModels/SupportViewModel.cs`
- Modify: `src/Superpowers.Launcher.Supervisor/Readiness/StardewReadinessEvaluator.cs`
- Modify: `src/Superpowers.Launcher.Supervisor/State/RuntimeHealthFact.cs`
- Modify: `src/Superpowers.Launcher.Supervisor/State/RecoveryEntryRef.cs`
- Modify: `src/Superpowers.CloudControl/Launch/LaunchDecisionController.cs`
- Modify: `src/Superpowers.CloudControl/Support/SupportTicketController.cs`
- Modify: `src/Superpowers.CloudControl/Narrative/HostedNarrativeController.cs`
- Modify: `src/Superpowers.Runtime.Local/Endpoints/RemoteDirectEndpoint.cs`
- Modify: `src/Superpowers.Runtime.Local/Endpoints/GroupChatEndpoint.cs`
- Modify: `games/stardew-valley/Superpowers.Stardew.Mod/Exposure/BuildExposureConfig.cs`
- Test: `tests/Superpowers.Launcher.Tests/Support/SupportViewModelTests.cs`
- Test: `tests/Superpowers.Runtime.Tests/Integration/ReadinessRecoveryIntegrationTests.cs`

**Wave:** Sequential

**Dependencies:** Task 7, Task 8

**Parallel-safe:** No

**Agent ownership:** `src/Superpowers.Launcher/ViewModels/StardewGameConfigViewModel.cs`, `src/Superpowers.Launcher/ViewModels/SupportViewModel.cs`, `src/Superpowers.Launcher.Supervisor/Readiness/StardewReadinessEvaluator.cs`, `src/Superpowers.Launcher.Supervisor/State/RuntimeHealthFact.cs`, `src/Superpowers.Launcher.Supervisor/State/RecoveryEntryRef.cs`, `src/Superpowers.CloudControl/Launch/LaunchDecisionController.cs`, `src/Superpowers.CloudControl/Support/SupportTicketController.cs`, `src/Superpowers.CloudControl/Narrative/HostedNarrativeController.cs`, `src/Superpowers.Runtime.Local/Endpoints/RemoteDirectEndpoint.cs`, `src/Superpowers.Runtime.Local/Endpoints/GroupChatEndpoint.cs`, `games/stardew-valley/Superpowers.Stardew.Mod/Exposure/BuildExposureConfig.cs`, `tests/Superpowers.Launcher.Tests/Support/SupportViewModelTests.cs`, `tests/Superpowers.Runtime.Tests/Integration/ReadinessRecoveryIntegrationTests.cs`

**Merge checkpoint:** `dotnet build src/Superpowers.sln && dotnet test tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj`

**Workspace isolation:** Sequential task in main worktree

- [x] **Step 1: Reconcile Task 7 and Task 8 by updating only the listed readiness/recovery files**
- [x] **Step 2: Write failing tests for shared `recoveryEntryRef` consumption, player-visible `failureClass` mapping, and support submission / receipt / diagnostic-failure behavior**
- [x] **Step 3: Update the launcher view model, supervisor state, cloud support path, and exposure config so support and recovery results round-trip cleanly**
- [x] **Step 4: Run the full build and full test suite**

Run: `dotnet build src/Superpowers.sln && dotnet test tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj`  
Expected: PASS.

- [x] **Step 5: Commit**

```powershell
git add src/Superpowers.Launcher/ViewModels/StardewGameConfigViewModel.cs src/Superpowers.Launcher/ViewModels/SupportViewModel.cs src/Superpowers.Launcher.Supervisor/Readiness/StardewReadinessEvaluator.cs src/Superpowers.Launcher.Supervisor/State/RuntimeHealthFact.cs src/Superpowers.Launcher.Supervisor/State/RecoveryEntryRef.cs src/Superpowers.CloudControl/Launch/LaunchDecisionController.cs src/Superpowers.CloudControl/Support/SupportTicketController.cs src/Superpowers.CloudControl/Narrative/HostedNarrativeController.cs src/Superpowers.Runtime.Local/Endpoints/RemoteDirectEndpoint.cs src/Superpowers.Runtime.Local/Endpoints/GroupChatEndpoint.cs games/stardew-valley/Superpowers.Stardew.Mod/Exposure/BuildExposureConfig.cs tests/Superpowers.Launcher.Tests/Support/SupportViewModelTests.cs tests/Superpowers.Runtime.Tests/Integration/ReadinessRecoveryIntegrationTests.cs
git commit -m "feat: wire readiness recovery and support flows end to end"
```

## Task 10: Manual Verification Closeout

**Files:**
- Modify: `docs/superpowers/governance/current-phase-boundary.md`
- Modify: `docs/superpowers/governance/evidence-review-index.md`
- Modify: `docs/superpowers/governance/evidence/stardew-m1-core-hand-check.md`
- Modify: `docs/superpowers/governance/evidence/stardew-implementation-only-channel-hand-check.md`
- Modify: `docs/superpowers/governance/evidence/stardew-player-visible-check.md`
- Modify: `docs/superpowers/governance/evidence/assets/2026-03-28-stardew-current-head/stardew-player-visible-proof.md`
- Modify: `docs/superpowers/governance/evidence/prompt-asset-protection.md`
- Modify: `docs/superpowers/governance/evidence/client-package-check.md`
- Modify: `docs/superpowers/governance/evidence/independent-review-record.md`
- Modify: `docs/superpowers/governance/evidence/release-governance-gate-record.md`
- Modify: `docs/superpowers/governance/evidence/degraded-window-proof.md`
- Modify: `docs/superpowers/governance/evidence/gate-time-runtime-degradation-recovery-evidence.md`
- Modify: `docs/superpowers/governance/reviews/2026-03-28-phase-boundary-rc-review.md`
- Modify: `docs/superpowers/governance/reviews/2026-03-28-product-claim-waiver-rc-review.md`
- Modify: `docs/superpowers/governance/reviews/2026-03-28-client-exposure-rc-review.md`
- Modify: `docs/superpowers/governance/reviews/2026-03-28-afw-boundary-rc-review.md`

**Wave:** Sequential

**Dependencies:** Task 9

**Parallel-safe:** No

**Agent ownership:** evidence and manual-verification rows only

**Merge checkpoint:** `dotnet build src/Superpowers.sln && dotnet test tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj`

**Workspace isolation:** Sequential task in main worktree

- [x] **Step 1: Rewrite the closeout workflow to `solo operator + manual verification only`**
- [x] **Step 2: Refresh the current candidate baseline in the manual verification docs**
- [x] **Step 3: Re-run build, test, publish, runtime health, mod sync, SMAPI load, and hosted narrative path verification**
- [x] **Step 4: Record current limitations instead of inventing approval gates**
- [x] **Step 5: Commit**

Run:
`dotnet build src/Superpowers.sln && dotnet test tests/Superpowers.Launcher.Tests/Superpowers.Launcher.Tests.csproj && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj && dotnet test tests/Superpowers.Stardew.Mod.Tests/Superpowers.Stardew.Mod.Tests.csproj`

And:
`powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-launcher.ps1 && powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-runtime-local.ps1 && powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-cloud-control.ps1 && powershell -ExecutionPolicy Bypass -File .\scripts\dev\publish-stardew-mod.ps1 -ProjectPath .\games\stardew-valley\Superpowers.Stardew.Mod\Superpowers.Stardew.Mod.csproj -OutputDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod && powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-runtime-local.ps1 && powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-cloud-control.ps1 && powershell -ExecutionPolicy Bypass -File .\scripts\dev\check-http-health.ps1 -Url http://127.0.0.1:5051/healthz && powershell -ExecutionPolicy Bypass -File .\scripts\dev\check-http-health.ps1 -Url http://127.0.0.1:7061/healthz && powershell -ExecutionPolicy Bypass -File .\scripts\dev\sync-stardew-mod.ps1 -RequireManifest -SourceDir .\artifacts\stardew-mod\Superpowers.Stardew.Mod -TargetDir 'D:\Stardew Valley\Stardew Valley.v1.6.15\Mods\Superpowers.Stardew.Mod' && powershell -ExecutionPolicy Bypass -File .\scripts\dev\run-stardew-smapi.ps1 -SmapiPath 'D:\Stardew Valley\Stardew Valley.v1.6.15\StardewModdingAPI.exe' -LogPath .\artifacts\logs\smapi-latest.log -TimeoutSec 120 -KillOnTimeout && dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "FullyQualifiedName~HostedNarrativePathTests"`

Expected:
- PASS for the repo-local verification chain
- no claim of `RC`, `GA`, sign-off, or external release approval
