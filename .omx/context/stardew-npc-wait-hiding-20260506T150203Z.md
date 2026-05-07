# Deep Interview Context: Stardew NPC Wait Hiding

Task statement: User proposes aggressively removing `wait` from the parent agent action surface, except for obvious host-determinable pause states: player menu open, NPC moving, festival, cutscene, sleep, day-end settlement, existing command running.

Desired outcome: Reduce wasted cloud-model turns, make NPCs act more instead of repeatedly choosing `wait + speak`, and increase local executor usefulness.

Stated solution: Hide `wait` from agent; host directly waits for obvious non-actionable states.

Intent hypothesis: Current parent model uses `wait` as a safe escape hatch. Recent logs show 37 intents after proxy fix: 35 wait, 2 move, 17 speak, only 2 local model calls. This makes local small model workload tiny and burns DeepSeek tokens.

Known facts/evidence:
- `.omx/specs/stardew-npc-local-executor-minimal-test.md` v1 hard-routes `move/observe/wait/task_status`, not `speak`.
- Current code host-interprets `wait`; local model is called for `move/observe/task_status` only.
- Current parent contract includes `action=move|observe|wait|task_status|escalate` plus optional `speech`.
- Current code submits `speech` from parent contract as host action after local executor result.
- Latest runtime after proxy disabled shows parent model stable, but wait-heavy behavior.

Constraints:
- No code changes during deep-interview.
- Preserve design boundary: parent makes high-level decisions; host handles obvious execution gating; local executor handles mechanical low-risk tools.
- Avoid turning NPC into a silent script system.

Unknowns/open questions:
- Should `wait` disappear entirely from parent contract, or remain only as internal host result/local executor outcome?
- What should parent do when no actionable option is available if `wait` is hidden: `observe`, `task_status`, or `escalate`?
- Should `speech` be allowed when host auto-waits?
- What acceptance ratio is desired for move/observe/task_status vs speak/wait?

Decision-boundary unknowns:
- Can implementation remove/rename contract fields and update tests without separate confirmation?
- Should private chat keep separate behavior from autonomy?
- Should host auto-wait suppress LLM calls entirely or just coalesce/cooldown them?

Likely codebase touchpoints:
- `src/runtime/NpcAutonomyLoop.cs`
- `src/runtime/NpcLocalActionIntent.cs`
- `src/runtime/NpcLocalExecutorRunner.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `Desktop/HermesDesktop.Tests/Runtime/*`

Prompt-safe initial-context summary status: not_needed
