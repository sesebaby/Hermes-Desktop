# Stardew Host-Owned Task Status Context

## Task statement
Implement the agreed design correction: AI should not remember or author runtime IDs such as commandId. Stardew task/status continuity should be host-owned, with AI-visible schemas containing only business fields.

## Desired outcome
- stardew_task_status can be called with no arguments to query the current pending/action slot or last terminal command known by the host.
- commandId remains an internal/log/UI/test identity, not a required model-authored field.
- no-tool/free-text JSON responses do not create action_submitted or command_terminal records with empty commandId.

## Evidence
- .omx/specs/星露谷主Agent与宿主任务Runner统一编排方案.md: visible tool call -> host/bridge task runner -> facts, no hidden executor.
- openspec/errors/E-2026-0511-stardew-hidden-local-executor-fallback.md: no-tool/free text must never execute through hidden local executor.
- openspec/errors/E-2026-0512-stardew-autonomy-text-json-tool-call-self-check.md: text JSON is final text, not a tool call.
- openspec/errors/E-2026-0513-stardew-private-chat-runtime-context-as-ai-field.md: host-known runtime identity must be injected by host/tool binding.
- src/games/stardew/StardewNpcTools.cs currently requires StardewTaskStatusToolParameters.CommandId and schema requires commandId.
- src/runtime/NpcAutonomyLoop.cs prompt still tells agent commandId is required, and action continuity reads commandId from tool result JSON.

## Constraints
- Do not add dependencies.
- Do not add local executor, JSON text parsing, phrase matching, or hidden fallback lanes.
- Keep one current path: visible tools, host task runner, facts back to agent.
- Keep commandId in internal results/logs where useful, but remove it from required AI inputs.

## Unknowns/open questions
- Whether optional by-id lookup should remain. Current plan keeps optional commandId for diagnostics/historical lookup but not required or prompt-driven.

## Likely touchpoints
- src/games/stardew/StardewNpcTools.cs
- src/runtime/NpcAutonomyLoop.cs
- Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs
- Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs
- skills/gaming/stardew-task-continuity/SKILL.md and related skill copy if tests require prompt text updates.
