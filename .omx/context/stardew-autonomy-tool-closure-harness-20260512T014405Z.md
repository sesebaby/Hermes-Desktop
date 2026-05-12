# Stardew Autonomy Tool Closure Harness Context

Task: align Hermes Stardew autonomy with the reference project after manual testing showed Haley could move to the beach, then later replied with plain JSON text such as `{"tool":"stardew_status"}` without issuing a real tool call.

Desired outcome:
- Parent autonomy remains agent-native.
- Real world writes and task updates continue to require visible Hermes/Stardew tool calls.
- When an active todo follows a completed world action and the first autonomy turn has no real tool call or explicit `wait:`/`no-action:` reason, the host runs one bounded self-check turn.
- The self-check explains that JSON text is not a tool call and asks the agent to close the loop through a visible tool call or explicit no-action reason.

Evidence:
- Runtime evidence from manual testing: movement command completed, then later LLM turns returned text JSON with `toolCalls=0`.
- `src/runtime/NpcAutonomyLoop.cs` currently logs `task_continuity_unresolved` for no-tool replies but does not re-enter the agent turn.
- `src/games/stardew/StardewPrivateChatOrchestrator.cs` already uses a bounded self-check contract for private chat.
- `external/hermescraft-main/bot/server.js` returns short lifecycle state through `briefState()` fields such as `task_done`, `task_error`, and `task_stuck`.

Constraints:
- Do not parse parent free-text or JSON text as an action.
- Do not route ordinary autonomy turns into hidden local executor fallback.
- Do not require global `tool_choice=required`.
- Do not complete todo automatically from terminal command status.
- Keep the change scoped to autonomy harness behavior and tests.

Likely touchpoints:
- `src/runtime/NpcAutonomyLoop.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`

