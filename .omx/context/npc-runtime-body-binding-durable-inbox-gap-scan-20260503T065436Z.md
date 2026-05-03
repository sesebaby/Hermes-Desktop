# NPC runtime body binding + durable inbox gap scan

## Task statement

Before executing the deeper Stardew NPC runtime fix, verify whether there are missing functional pieces beyond:
- formally adding `smapiName` / `targetEntityId` into runtime/body binding instead of relying on bridge alias fallback;
- routing cron due events into an NPC runtime durable inbox / pending work item instead of directly submitting `open_private_chat` from `StardewNpcAutonomyBackgroundService`.

## Desired outcome

Produce a grounded consensus plan that identifies all execution-critical gaps, orders them by dependency, and avoids a narrow "headache medicine" fix.

## Known facts / evidence

- Desktop and NPC capability composition is already mostly unified through `AgentCapabilityAssembler`; NPC private chat and autonomy handles call `AgentCapabilityAssembler.RegisterAllTools(...)`.
- `NpcRuntimeDescriptor` still contains `NpcId`, `DisplayName`, `GameId`, `SaveId`, `ProfileId`, `AdapterId`, `PackRoot`, and `SessionId`, but not `SmapiName` or `TargetEntityId`.
- `NpcPackManifest` validates `smapiName` and `targetEntityId`, but `NpcRuntimeDescriptorFactory.Create(...)` drops them when creating runtime descriptors.
- Stardew command envelopes still send `action.NpcId` as `npcId`; bridge-side `BridgeNpcResolver` currently resolves lowercase IDs like `haley` as an alias fallback.
- Runtime controller state exists: `NpcRuntimeDriver`, `NpcRuntimeStateStore`, `PendingWorkItem`, `ActionSlot`, `NextWakeAtUtc`, and event cursor persistence.
- Host staging exists: `StardewRuntimeHostStateStore` stores source cursor and one staged batch, then commits after worker dispatch.
- Per-NPC worker dispatch exists inside `StardewNpcAutonomyBackgroundService.NpcAutonomyTracker`, but it is still a nested Stardew service implementation, not a reusable runtime inbox/driver abstraction.
- Cron due handling is currently direct: `HandleCronTaskDueAsync(...)` matches NPC session and calls `SubmitScheduledPrivateChatAsync(...)`, which submits `GameActionType.OpenPrivateChat` immediately.
- Core Stardew DTOs support `seq` / `next_seq`, but SMAPI bridge `BridgeCommandModels.EventPollQuery`, `BridgeEventData`, and `EventPollData` do not yet expose `seq` / `next_seq`; the bridge currently only encodes order in `evt_000...` IDs.
- `StardewPrivateChatBackgroundService` is no longer registered; private chat is processed through `StardewPrivateChatRuntimeAdapter` inside the autonomy host loop.

## Constraints

- Keep Desktop and NPC on the same capability composition path; do not introduce a separate Stardew/NPC prompt or tool assembler.
- Preserve single Desktop process hosting multiple NPC runtimes.
- Keep diffs small and test-first where behavior is not already locked.
- Do not revert existing uncommitted changes.

## Unknowns / open questions

- Whether to add `smapiName` / `targetEntityId` directly to `NpcRuntimeDescriptor` or introduce a general body-binding record on the descriptor.
- Whether cron due should reuse the existing `PendingWorkItem` shape or require a first-class durable inbox item model.
- Whether host staging must wait for durable per-NPC inbox append before committing source cursor; current implementation waits for worker completion, but not a distinct inbox append contract.
- Whether bridge event sequence support should be implemented in the same execution slice or treated as a prerequisite slice.

## Likely codebase touchpoints

- `src/runtime/NpcRuntimeDescriptor.cs`
- `src/runtime/NpcRuntimeDescriptorFactory.cs`
- `src/runtime/NpcRuntimeDriver.cs`
- `src/runtime/NpcRuntimeStateStore.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/runtime/NpcRuntimeBindings.cs`
- `src/games/stardew/StardewNpcRuntimeBindingResolver.cs`
- `src/games/stardew/StardewCommandService.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/games/stardew/StardewRuntimeHostStateStore.cs`
- `src/games/stardew/StardewBridgeDtos.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeCommandModels.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeEventBuffer.cs`
- `Mods/StardewHermesBridge/Bridge/BridgeHttpHost.cs`
- `Desktop/HermesDesktop.Tests/Runtime/*`
- `Desktop/HermesDesktop.Tests/Stardew/*`
- `Mods/StardewHermesBridge.Tests/*`
