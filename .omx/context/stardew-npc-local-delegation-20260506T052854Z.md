# Stardew NPC Local Delegation Context

## Task Statement

Clarify the intended architecture for keeping the main Stardew NPC agent context minimal while routing intermediate operations such as move planning, world scouting, and tool-use preparation to a local small model or child agent.

## Desired Outcome

Produce an execution-ready specification for whether Stardew autonomy ticks should include an explicit planner/child-agent step, what that step owns, what remains with the main NPC agent, and how to test that the architecture actually reduces main-agent context while preserving NPC behavior quality.

## Stated Solution

The user wants the main agent context to stay as short as possible. Intermediate operations such as `move` and similar tool-facing steps should be delegated to a local small model. The user wants to discuss whether adding an explicit planner/child-agent step inside the Stardew autonomy tick is reasonable, instead of relying on the main model to voluntarily call the generic `agent` tool.

## Probable Intent Hypothesis

The likely goal is to split high-level NPC intent/personality from low-level world/action execution work. The main NPC agent should decide intent with minimal context, while local child agents handle bounded planning or operational details to reduce cost, latency, prompt bloat, and risk of the main agent narrating actions instead of using game tools.

## Known Facts / Evidence

- Commit `2cf95ac7` added configurable LLM lanes for root `model`, `stardew_autonomy`, `stardew_private_chat`, and `delegation`.
- `Desktop/HermesDesktop/App.xaml.cs` wires Stardew autonomy/private chat to lane clients and passes the delegation lane into runtime composition.
- `src/runtime/AgentCapabilityAssembler.cs` registers `AgentTool` with `DelegationChatClient ?? ChatClient`.
- `src/Tools/AgentTool.cs` is still a generic flat single-child v1 tool, with agent types such as `researcher`, `coder`, `analyst`, `planner`, `reviewer`, and `general`.
- `src/runtime/NpcAutonomyLoop.cs` currently sends the decision message to the NPC agent. The message instructs the model to call `stardew_move` when movement is needed.
- `stardew_move` is currently a tool available to the main NPC runtime; there is no automatic pre-move child-agent orchestration yet.
- The current implementation solves "which model/client child delegation uses", not "when and how movement gets delegated".

## Constraints

- Keep host boundary intact: game side and bridge expose facts/tools; they must not replace NPC decision-making.
- Each NPC keeps independent home/session/memory boundaries.
- No second personality/memory lane or host-authored NPC decisions.
- First stage should avoid broad UI/settings scope unless needed for testability.
- Any architecture must be observable with logs and testable without relying only on emergent model behavior.

## Unknowns / Open Questions

- What is the primary non-negotiable goal: shorter main-agent context, lower model cost/latency, more reliable tool execution, or stronger NPC personality coherence?
- Should child agents be mandatory for selected action classes or optional/advisory?
- Should a child agent output final tool calls, structured action intents, or only a compact plan that the main agent approves?
- Which operations count as "middle work" in v1: move only, observe/pathfind, task continuation, private chat triage, or all action selection?
- How much autonomy can the host planner have before it violates the "host does not decide for NPC" boundary?

## Decision-Boundary Unknowns

- Whether implementation should enforce delegation for `move` even if the main NPC could call `stardew_move` directly.
- Whether local small model output may be allowed to trigger host-executed tool calls directly.
- Whether main NPC should see child-agent details, summaries only, or no child transcript.
- Whether latency from extra LLM calls is acceptable on every tick or only on complex/uncertain actions.

## Likely Codebase Touchpoints

- `src/runtime/NpcAutonomyLoop.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/runtime/AgentCapabilityAssembler.cs`
- `src/Tools/AgentTool.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `Desktop/HermesDesktop.Tests/Stardew/*Autonomy*Tests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcRuntimeSupervisorTests.cs`
