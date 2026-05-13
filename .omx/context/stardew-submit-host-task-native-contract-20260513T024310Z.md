# Stardew Submit Host Task Native Contract Context

## Task Statement

Fix live Stardew private-chat movement where the parent NPC agent calls `stardew_submit_host_task` but the call fails before host-task ingress because `target` is not shaped as the native tool contract expects.

## Desired Outcome

- Private-chat immediate world actions remain agent-native: visible tool call -> structured tool result -> host task ingress -> bridge execution -> terminal fact.
- Invalid `target` values return a clear `ToolResult.Fail` from `stardew_submit_host_task` instead of a generic dispatcher JSON exception.
- A failed `stardew_submit_host_task` call does not count as a successful host-task submission for private-chat self-checks.
- No hidden local executor, natural-language target parser, phrase table, coordinate guessing, or second execution lane is introduced.

## Known Facts / Evidence

- Live logs showed `stardew_submit_host_task` failed during `System.Text.Json` conversion at `$.target`.
- The successful later beach movement came from autonomy via `stardew_navigate_to_tile`, not from the failed private-chat submission.
- `src/Core/Agent.cs` deserializes tool arguments into `tool.ParametersType` before calling `tool.ExecuteAsync`.
- `src/games/stardew/StardewNpcTools.cs` currently uses `StardewSubmitHostTaskMoveTargetParameters? Target`, so malformed target shapes can fail before the tool can return contract guidance.
- `src/games/stardew/StardewPrivateChatOrchestrator.cs` currently checks only assistant tool-call presence for self-check decisions.

## Constraints

- Stardew v1 has one execution path: parent/main NPC agent calls model-visible Hermes/Stardew tool; host/bridge mechanically executes; terminal facts return to the agent.
- Do not parse `"beach"`, `"海边"`, or other natural-language destinations in host code.
- Do not reintroduce `local_executor`, hidden JSON/free-text execution, or compatibility fallback.
- `todo` remains agent-owned; terminal facts must not auto-close todos.
- Align with reference projects: Hermes agent native tool-call/result loop and HermesCraft task queue/currentTask/status fact feedback.

## Unknowns / Open Questions

- None blocking. The repair is bounded to tool argument validation and private-chat self-check success gating.

## Likely Codebase Touchpoints

- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
- `.omx/state/autopilot-state.json`

