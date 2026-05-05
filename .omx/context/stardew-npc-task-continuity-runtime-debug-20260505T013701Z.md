# Deep Interview Context Snapshot: Stardew NPC Task Continuity Runtime Debug

UTC timestamp: 2026-05-05T01:37:01Z

## Task Statement

Run `$deep-interview` against:

`D:\GitHubPro\Hermes-Desktop\docs\superpowers\specs\2026-05-05-星露谷NPC任务连续性与运行时调试设计.md`

The provided design concerns Stardew NPC task continuity, failure feedback, long-running NPC runtime sessions, and a desktop NPC agent runtime debugging workspace.

## Desired Outcome

Produce an execution-ready clarified requirements artifact before implementation or planning handoff. The interview should resolve remaining ambiguity around intent, non-goals, decision boundaries, constraints, and acceptance criteria, then hand off to `$ralplan`, `$autopilot`, `$ralph`, `$team`, or refinement.

## Stated Solution

The draft proposes:

- Teach Stardew NPC agents to convert player commitments and plans into durable `todo` / `memory` / `session_search` usage through prompt, skill, and tool behavior instead of host-side keyword rules.
- Keep each NPC's task continuity on its long-term runtime session (`descriptor.SessionId`), while preserving private chat transcript granularity.
- Adjust private chat and autonomy prompts so NPCs can use tools before replying and can resume long-running tasks after interruptions.
- Add a Stardew task-continuity skill asset.
- Extend `NpcRuntimeWorkspaceService` and the desktop agent UI into an NPC runtime workbench that observes task/runtime state without creating a second source of truth.
- Ensure failed or blocked tasks cause immediate in-game feedback and desktop UI traceability.

## Probable Intent Hypothesis

The user likely wants to prevent a misaligned implementation that hardcodes Stardew-specific task rules in the host. The deeper intent is to preserve Hermes as an agent framework where NPCs use existing Hermes-native skills, memory, todo, session search, and tools to maintain continuity, while making the runtime observable enough to debug whether that agentic behavior is actually happening.

## Known Facts / Evidence

- Repository is brownfield: `D:\GitHubPro\Hermes-Desktop` has existing Stardew, runtime, desktop UI, tests, and `.omx` directories.
- Git worktree already has unrelated/staged user changes and the target spec is currently untracked.
- The target spec already contains a substantial design draft with non-goals, constraints, implementation touchpoints, tests, risks, and reference-project evidence.
- `omx question --help` failed because `omx` is not recognized as a command.
- `Get-Command omx -All` returned no result.
- Searching `C:\Users\Administrator\.codex` and the repository found no executable `omx*` entry, except an old `.omx/logs/omx-2026-04-27.jsonl`.
- No OMX-related environment variables were present.

## Constraints

- Deep Interview requires `omx question` for every interview round.
- If `omx question` is unavailable, the skill explicitly requires stopping and surfacing this as a blocker instead of falling back to plain-text questions or `request_user_input`.
- Do not implement inside deep-interview mode.
- Project instructions require Chinese user-facing progress/final reports by default.
- Existing user changes must not be reverted or overwritten.
- The project forbids host-side NPC decision logic that replaces Hermes agent autonomy.

## Unknowns / Open Questions

- Whether the user wants to install/enable OMX CLI runtime in this shell before continuing the formal interview.
- Whether the current spec should be treated as already close to planning-ready or should undergo the full standard interview once `omx question` is available.
- Which decision boundaries the user wants to delegate to OMX versus reserve for explicit confirmation.
- Whether UI runtime workbench scope should be MVP-only or include richer trace/error/archive/lease/action slot details in the first pass.

## Decision-Boundary Unknowns

- Can OMX decide the exact prompt/skill wording without confirmation if it preserves the no-hardcoded-rules constraint?
- Can OMX decide the private-chat transcript vs long-term task session split implementation details?
- Can OMX decide the desktop UI layout and minimal runtime workbench fields?
- Can OMX defer parts of runtime observability if prompt/session continuity is the critical path?

## Likely Codebase Touchpoints

- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs`
- `src/runtime/NpcRuntimeContextFactory.cs`
- `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
- `Desktop/HermesDesktop/Views/AgentPage.xaml`
- `Desktop/HermesDesktop/Views/AgentPage.xaml.cs`
- `Desktop/HermesDesktop/Views/Panels/AgentPanel.xaml`
- `Desktop/HermesDesktop/Views/Panels/AgentPanel.xaml.cs`
- `skills/gaming/stardew-task-continuity/SKILL.md`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcPrivateChatAgentRunnerTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`

## Blocker

Formal deep-interview cannot proceed in the current shell because `omx question` is unavailable. This snapshot was created to preserve preflight context for resume.
