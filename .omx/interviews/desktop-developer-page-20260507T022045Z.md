# Deep Interview Transcript: Desktop Developer Page / NPC Inspector

## Metadata

- Interview id: `desktop-developer-page-20260507T022045Z`
- Profile: Standard
- Context type: Brownfield
- Final ambiguity: 15.5%
- Threshold: 20%
- Context snapshot: `.omx/context/desktop-developer-page-20260507T020104Z.md`
- Residual risk: OMX `omx question` renderer failed in this Codex session because the process was not attached to a tmux pane. The user explicitly allowed continuing without the tool earlier, then asked that the final document follow `$deep-interview` artifact structure. This transcript preserves the direct-chat clarification path and marks the channel deviation.

## Original Task

The user wants to modify the Hermes Desktop UI by adding or reshaping a developer-facing page. The page must reuse existing backend/runtime infrastructure and must not create a parallel implementation path.

## Condensed Transcript

### Round 0: Triggering problem

The user reported that the desktop app has no obvious place to directly interact with Haley's NPC agent. Repository inspection showed that the current desktop UI has:

- `ChatPage`: global Hermes chat, not NPC-specific.
- `AgentPage > Runtime`: NPC runtime list and fixed debug buttons.
- `DashboardPage`: read-only status cards and a shortcut to Agent Runtime.
- Backend support for NPC runtime, private chat runner, autonomy debug tick, and debug speak services.

Evidence-based conclusion: the missing surface is primarily a UI/information architecture problem, not a missing backend capability.

### Round 1: Scope recommendation

The user invoked `$deep-interview` to use this opportunity to add a developer page. The first structured question could not be delivered through `omx question` due the tmux renderer failure. The assistant still prepared the intended scope choices:

- NPC debug workbench.
- General system diagnostics.
- Direct NPC chat entry.
- Lightweight developer hub.

The user later clarified that their dissatisfaction is with the current UI, and that the solution should reuse existing backend infrastructure while adding or reshaping the frontend UI.

### Round 2: Proposed direction

The assistant recommended an independent Developer Workbench page rather than overloading Agent Runtime:

- Add a left-nav Developer page.
- Reuse `NpcRuntimeWorkspaceService`, `StardewNpcDebugActionService`, `StardewAutonomyTickDebugService`, existing logs, existing runtime state, and existing registration in `App.xaml.cs`.
- Do not create a second NPC runtime, second private chat path, second local executor path, or separate task/todo store.

The user asked for an ASCII sketch and then accepted the overall direction.

### Round 3: Pressure pass / missing observability

The user sharpened the scope: they need to see NPC memory documents, `SOUL.md`, AI API replies, tool-call process, subagent delegation and interaction, tasks, and todos. The purpose is direct problem discovery, not just buttons.

The assistant reframed the page as an NPC black-box inspector: a trace-centered view that reconstructs one NPC decision from observation through context, model response, tool calls, delegation, bridge command, and result.

### Round 4: UI language constraint

The user approved the direction but added a strict UI language rule:

- User-facing UI must not mix Chinese and English.
- Labels such as `task/TODO diff/replay` are not acceptable.
- The UI should be all Chinese or all English; for this feature, use Chinese.
- Raw logs, model responses, file contents, tool arguments, and other original artifacts may remain as-is.

This resolves a key non-goal and acceptance criterion for the UI copy.

### Round 5: Process boundary

The user stopped implementation and requested a plan document first. They then clarified that the plan should follow `$deep-interview` requirements. No product UI code should be changed before the spec is reviewed.

## Clarity Score

| Dimension | Score | Justification |
| --- | ---: | --- |
| Intent | 0.90 | The user wants faster and more visual diagnosis of NPC behavior, not just another debug button page. |
| Outcome | 0.85 | Desired end state is a Chinese NPC inspector showing identity, memory, context, model response, tools, delegation, tasks, todos, and logs. |
| Scope | 0.80 | First version should focus on NPC/runtime observation; broader diagnostics can be staged. |
| Constraints | 0.90 | Reuse backend, no dual-track implementation, Chinese UI, raw logs exempt, no direct implementation before plan. |
| Success | 0.75 | Acceptance is clear at user level; exact data availability per trace still needs implementation-time verification. |
| Context | 0.80 | Existing UI and service touchpoints are identified; raw AI/tool visibility storage details need deeper code mapping. |

Brownfield formula:

`1 - (0.90*0.25 + 0.85*0.20 + 0.80*0.20 + 0.90*0.15 + 0.75*0.10 + 0.80*0.10) = 0.155`

Final ambiguity: 15.5%.

## Readiness Gates

- Non-goals: explicit.
- Decision boundaries: explicit enough for planning.
- Pressure pass: complete. The missing-observability challenge changed the page from a generic Developer Hub into a trace-centered NPC Inspector.

## Residual Risks

- The exact source for raw AI API request/response and tool-call payloads must be confirmed before implementation. If not already present in current logs/transcript, the implementation should add minimal diagnostics to the existing runtime log or transcript path, not a new storage system.
- The current `Agent > Runtime` tab overlaps with the proposed Developer page. The design should avoid long-term duplicate debug actions.
- Some Chinese UI strings will be longer than current English labels; layout must be designed for expansion.

