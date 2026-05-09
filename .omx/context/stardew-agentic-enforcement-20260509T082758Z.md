# Context: Stardew Agentic Enforcement

Task statement: fix Penny/Haley manual-test regressions without hardcoding locations. User wants prompt/orchestration/agentic contract fixes, with tests using real AI.

Desired outcome:
- Haley/private-chat parent: if the model accepts an immediate movement/world-action request, the turn must call npc_delegate_action, not only speak.
- Penny/local executor: stardew_navigate_to_tile must only be called with target data disclosed by loaded stardew-navigation skill references in the same local executor turn. Natural language labels such as 图书馆 must not pass through as mechanical locationName unless skill target evidence provided them.

Known evidence:
- SMAPI/runtime logs showed Penny enqueued move then failed with location_not_found:图书馆.
- Logs showed Haley completed private chat speech but no task_move_enqueued after agreement.
- Current stardew-navigation skill already specifies parent delegation and executor skill-view flow.
- Current private chat prompt already says accepted immediate world action requires npc_delegate_action, but no runtime completion check enforces it.
- Current NpcLocalExecutorRunner reads target(...) cues from skill_view and narrows next iteration to stardew_navigate_to_tile, but it does not reject navigation tool calls made before a skill target cue is loaded.
- Reference repo external/hermes-agent-main validates tool surfaces and sends structured error results back to the model for self-correction when a tool call is invalid.

Constraints:
- Do not hardcode library/beach mappings.
- Do not host-parse conversational text into destinations.
- Preserve existing dirty user changes; keep diffs scoped.
- Tests must include real AI smoke coverage, but fast regression tests can use deterministic fakes for red/green where unavoidable.

Likely touchpoints:
- src/Core/Agent.cs: generic tool loop enforcement after no-tool final answer or tool validation.
- src/games/stardew/StardewPrivateChatOrchestrator.cs: private chat prompt/message and maybe runner/session inspection.
- src/runtime/NpcLocalExecutorRunner.cs: skill-target provenance and corrective tool-result loop.
- Desktop/HermesDesktop.Tests/Runtime/NpcLocalExecutorRunnerTests.cs.
- Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs.
- Desktop/HermesDesktop.Tests/Stardew/StardewLiveAiSmokeTests.cs.

Unknowns/open questions:
- Best location for private-chat no-delegate correction: generic Agent tool loop vs Stardew-specific runner wrapper. Prefer Stardew-specific to avoid broad behavior changes.
- Whether current OpenAiClient/tool loop supports forced tool_choice; likely no. Use prompt + retry/correction instead.
