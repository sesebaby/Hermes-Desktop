# Stardew Agent-Native Navigation Deep Interview Context

Timestamp: 2026-05-09T00:14:01Z
Profile: standard
Prompt-safe initial-context summary status: not_needed

## Task Statement

Clarify the desired architecture for Stardew NPC action delegation and cross-map navigation after the implemented navigation plan works through manual buttons but private chat promises such as "now go to the beach" do not result in movement.

## Desired Outcome

Define an execution-ready requirement spec that removes host-generated movement candidate surfaces and makes NPC action intent flow through agent-native delegation, local small-model execution, skills, and host executor calls.

## Stated Solution

The user wants the main/cloud agent to delegate action execution to a local small model. The local small model should use Stardew skills to obtain concrete location coordinates and then call the host executor/tool with an explicit mechanical target. The user explicitly rejects host-side hardcoded natural-language place parsing.

## Probable Intent Hypothesis

The underlying goal is to preserve the agent project architecture: agents understand player intent and world meaning through skills and tools, while the host only executes explicit mechanical actions. The user wants one durable movement/action lane instead of temporary compatibility paths that will become forgotten technical debt.

## Known Facts / Evidence

- Manual "send Haley to Beach" works because a debug/manual path submits a concrete `Beach:32,34` target with `targetSource=map-skill:stardew.navigation.poi.beach-shoreline`.
- Current parent autonomy tool surface was previously observed as effectively empty for this path.
- Current autonomy prompt has a compact `stardew-navigation` contract but does not preload full POI contents.
- `skills/gaming/stardew-navigation` contains layered navigation references including beach region and beach shoreline POI.
- The beach shoreline POI contains `target(locationName=Beach,x=32,y=34,source=map-skill:stardew.navigation.poi.beach-shoreline)`.
- Current deterministic local executor can navigate when an intent already carries a concrete target.
- Existing `stardew_move(destinationId)` path is tied to `destination[n]` style candidates, but the user now wants the `destination` concept removed completely from the NPC movement product path.
- Private chat currently replies / can persist todo-like state, but the unresolved requirement is how accepted immediate action requests become actionable delegation work without host natural-language parsing.

## Constraints

- Do not hardcode natural-language place parsing in the host.
- Treat Hermes as an agent project: prefer agent-native, skill/tool-driven behavior over host-side rules or script-like parsers.
- Fully reuse existing infrastructure before adding new routes.
- Avoid dual-track implementations. If a second path appears necessary, it must be explicitly approved by the user first.
- Do not give direct move authority to the main/cloud agent.
- Do not keep `destination[n]` as a parallel movement route.
- Remove the `destination` concept entirely from the NPC movement product path; if manual/debug movement survives, it must be coordinate/skill-source based rather than destination-id based.
- Pre-release product constraint: prefer one implementation path; avoid dual tracks and shadow compatibility systems.
- Host should not decide NPC behavior; it should provide facts and execute explicit tool/action contracts.
- Skills are Markdown assets and should remain the world/navigation/persona knowledge surface.
- Private chat action acceptance should be agent-driven, not a host parser guessing intent from text.

## Unknowns / Open Questions

- What exact contract should the private chat agent use when it accepts an immediate player request?
- Resolved Round 1: private chat should call a structured `npc_delegate_action`-style tool during the same LLM turn as the only main path for immediate action delegation.
- Resolved Round 2: the local executor model may use existing `skill_view` access to read `stardew-navigation` and linked references as the first-pass and only location knowledge source, then call executor-only `stardew_navigate_to_tile`.
- Resolved Round 3: failed skill lookup, ambiguous places, and navigation failures should return structured `blocked` / `escalate` back to the agent/player. The host must not fallback to natural-language parsing or `destination[n]`.
- Resolved Round 5: do not introduce complex priority semantics. Keep the agent contract simple: if the private-chat agent accepts an immediate action request, it calls `npc_delegate_action`; the runtime queues that delegated action through existing ingress/action-slot mechanics without asking the agent to manage numeric priority, timing policy, or interruption policy.
- Resolved Round 6: minimal observability is sufficient, but logs should be diagnostic-friendly so failures can be checked quickly. The user wants logs that make issue investigation easy, not just success markers.
- What observability is required to prove the agent used skills and delegated correctly?

## Interview Rounds

### Round 1

Question: Should private chat action delegation use a same-turn structured tool call (`npc_delegate_action`) or a structured assistant message consumed by a dispatcher?

User answer: Agreed to the recommended `npc_delegate_action` tool-call path.

Decision: The private chat agent should explicitly call a structured delegation tool when it accepts an immediate player action request. The host must not infer this by parsing conversational reply text.

Rationale: Tool calls reuse the existing Hermes tool/runtime infrastructure, provide stronger observability, and avoid a second implicit protocol.

### Round 2

Question: Should the local small model be allowed to use existing `skill_view` access to read the Stardew navigation skill and references, using that as the first-pass location knowledge source?

User answer: Allowed.

Decision: Local executor location resolution should reuse the existing skill infrastructure. For the first pass, navigation POIs and coordinates should come from `stardew-navigation` skill content and its linked reference files, not a new host-side resolver.

Rationale: This preserves the agent-native design, keeps world/location meaning in skills, and avoids creating a parallel destination parsing route.

### Round 3

Question: When the local small model cannot find a unique skill-grounded coordinate, sees ambiguity, or the navigation tool fails, should first-pass behavior return `blocked` / `escalate` instead of letting the host fallback to parsing or `destination[n]`?

User answer: Agreed.

Decision: The first implementation should not include host fallback location parsing or `destination[n]` fallback. Failures should remain visible as structured executor failures that the agent can explain or clarify with the player.

Rationale: This preserves a clean agent/host boundary and prevents a hidden second movement route from reappearing.

### Round 4

Question: Should `destination` be removed only from agent-facing paths while internal/manual debug movement keeps using destination IDs, or should the concept be removed completely from the NPC movement product path?

User answer: Remove it completely. Indoor movement will later be handled by the local small model as a micro-action; prior micro-action work already exists.

Decision: The `destination` concept should be removed completely from the NPC movement product path. Any surviving manual/debug entry points should be rewritten to use explicit coordinates or skill-derived targets, not destination IDs.

Rationale: This prevents a dormant compatibility path from becoming the real path again and keeps indoor motion aligned with the same agent-native executor approach.

### Round 5

Question: Should private-chat delegated actions carry explicit priority/timing/interruption fields for scheduling?

User answer: Do not make priority complex. Keep agent work simple and direct to reduce errors.

Decision: The first pass should avoid numeric priorities and complex scheduling semantics. A same-turn `npc_delegate_action` tool call from private chat should create a simple delegated action ingress item. The runtime should process that work item through existing ingress/action-slot mechanics when the executor is free, without making the agent manage priority policy.

Rationale: Simpler tool contracts reduce model error. The important distinction is that the agent explicitly delegated an action, not that it filled out a rich scheduler policy.

### Round 6

Question: Is minimal observability enough, or should the logs also be optimized for troubleshooting?

User answer: Accept minimal observability, but make the logs easy to inspect when debugging problems.

Decision: Log only the important proof points, but include enough structured context to debug failures quickly. The logs should show tool path, skill source, selected target, executor mode, command id, blocked reason, and trace id where available.

Rationale: This keeps the runtime visible without turning logs into a second agent reasoning channel.

## Decision-Boundary Unknowns

- What can Codex decide without confirmation when designing the first implementation plan?
- Resolved Round 4: remove the `destination` concept entirely from the NPC movement product path. Do not preserve `stardew_move(destinationId)` as a compatibility route unless the user later explicitly reopens it.
- Resolved Round 5: private-chat delegated actions should use a simple immediate delegated-action ingress path, with no numeric priority or complex interruption semantics.

## Likely Codebase Touchpoints

- `src/games/stardew/StardewPrivateChatOrchestrator.cs`
- `src/runtime/NpcRuntimeSupervisor.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs`
- `skills/gaming/stardew-navigation/SKILL.md`
- `skills/gaming/stardew-navigation/references/**`
- `Desktop/HermesDesktop.Tests/Stardew/**`
- `Mods/StardewHermesBridge/**`
