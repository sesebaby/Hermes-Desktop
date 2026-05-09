# Test Spec: Stardew Agent-Native Navigation Delegation

Status: draft for RALPLAN review
Related PRD: `.omx/plans/prd-stardew-agent-native-navigation.md`

## Unit Tests

- `NpcDelegateActionTool` creates a `npc_delegated_action` ingress item with trace id, conversation id, action, reason, and raw intent text.
- `NpcDelegateActionTool` rejects unsupported action values and empty reasons without parsing natural language.
- `NpcLocalActionIntent` no longer accepts `destinationId` as a valid move target.
- `NpcLocalExecutorRunner` no longer selects `stardew_move` for move resolution.
- `NpcLocalExecutorRunner` exposes only the local executor tool set needed for movement: read-only `skill_view` plus `stardew_navigate_to_tile` for this path.
- Local executor tool-surface fingerprint/rebind tests prove adding read-only `skill_view` changes executor binding deterministically and does not register `skill_manage`, `skill_invoke`, filesystem tools, or parent movement tools.
- `NpcLocalExecutorRunner` retries once on no tool call and returns `blocked` when skill lookup or target selection fails.

## Integration Tests

- Private chat agent with a fake tool call to `npc_delegate_action` records a delegated action ingress item and still returns visible reply text.
- `StardewNpcAutonomyBackgroundService` processes `npc_delegated_action` ingress before ordinary autonomy decision work when available, using existing action-slot constraints.
- A delegated beach movement causes local executor to read the navigation skill/reference chain and call `stardew_navigate_to_tile`.
- If the action slot is occupied, delegated work remains queued rather than being dropped or forcing interruption.
- Ambiguous target result writes a blocked record and does not call `stardew_navigate_to_tile`.

## Regression Tests

- No NPC prompt, compact skill contract, or tool description contains `destination[n]` as an action candidate surface.
- Bundled Stardew skill asset checks explicitly cover `stardew-navigation`, `stardew-world`, `stardew-task-continuity`, and `stardew-social`.
- `stardew_move(destinationId)` is absent from NPC movement routes and local executor selected tools.
- `StardewQueryService` no longer emits `destination[n]`, `nearby[n]`, or `moveCandidate[n]` facts into NPC movement observations for the product path.
- `StardewNpcAutonomyPromptSupplementBuilder` and prompt-budget tests no longer preserve `stardew_move` / destination candidate tool-call history as required movement context.
- Existing concrete-target move tests still pass with `stardew_navigate_to_tile`.
- Existing idle micro-action tests still pass and continue to forbid movement fields on micro-actions.
- Manual/debug movement, if retained, uses explicit coordinate/skill-source action rather than destination IDs.
- Bridge DTO / command-service `destinationId` tests may remain only if explicitly classified as lower-level legacy adapter coverage and not reachable from NPC private-chat/autonomy movement.

## Observability Tests

- Runtime log includes `actionType=private_chat_delegation` or equivalent when `npc_delegate_action` is called.
- Runtime log includes work item id and trace id when delegated action is queued.
- Local executor log includes skill source/file path and selected target source.
- Local executor result log includes executor mode, tool name, command id if present, and blocked reason if present.
- Blocked target-resolution log includes enough context to tell whether failure was missing skill, ambiguous POI, no tool call, or navigation tool failure.

## Verification Commands

Run focused tests first:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcPrivateChatAgentRunnerTests|FullyQualifiedName~NpcLocalExecutorRunnerTests|FullyQualifiedName~NpcLocalActionIntentTests|FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests|FullyQualifiedName~StardewNpcToolFactoryTests"
```

Run migration guard tests / searches:

```powershell
rg -n "destination\\[|destinationId|moveCandidate|nearby\\[|stardew_move" skills\gaming src\runtime src\games\stardew Desktop\HermesDesktop.Tests\Runtime Desktop\HermesDesktop.Tests\Stardew
```

Any remaining hits must be either removed from the NPC product path or explicitly documented as lower-level legacy adapter coverage outside this migration.

Then run the main test project:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

If bridge DTO/action contracts are changed, also run:

```powershell
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
```
