# PRD: Stardew Submit Host Task Native Contract

## Decision

Repair `stardew_submit_host_task` by moving `target` shape validation into the Stardew native tool and by making private-chat self-checks distinguish successful host-task submissions from failed tool attempts.

## Drivers

- Agent-native boundary: only visible tool calls can authorize world writes.
- Contract observability: invalid tool arguments must produce model-visible tool results, not host stack traces.
- Reference alignment: task execution follows queue/current task/status fact feedback; no hidden executor guesses intent.
- Minimality: fix the failing private-chat host-task path without widening schemas or changing autonomy/todo semantics.

## Alternatives Considered

- Host parses natural-language target strings such as `beach` or `海边`.
  Rejected because it makes the host a hidden map resolver and repeats prior local-executor boundary failures.
- Global Agent dispatcher catches all deserialization failures and retries with raw JSON.
  Rejected because it broadens behavior for every tool and hides a Stardew-specific contract problem in the core loop.
- Force `target` to remain strongly typed and rely on prompt wording.
  Rejected because prompt wording already failed in live logs and the tool boundary must enforce executable contracts.

## Scope

- Change `StardewSubmitHostTaskToolParameters.Target` to preserve raw JSON shape so `ExecuteAsync` can validate and return structured failure.
- Accept only object-shaped mechanical targets with `locationName`, integer `x`, integer `y`, and `source`.
- Preserve optional `facingDirection`.
- Queue host-task ingress only after validation passes.
- Update private-chat self-check logic so failed submit calls do not satisfy the “host task submitted” gate.
- Apply the same successful-result gate to `npc_no_world_action`; a failed no-world declaration is not a valid closure.
- Keep result-body parsing isolated in one helper until `ToolResult` gains durable success metadata in session messages.

## Out Of Scope

- No automatic todo closure after terminal facts.
- No bridge movement/pathfinding changes.
- No support for free-text destination strings or inferred POIs.
- No `local_executor` or secondary task lane revival.

## Acceptance Criteria

- Malformed `target` for `stardew_submit_host_task` returns `ToolResult.Fail` and queues no ingress.
- A string `target` does not throw from `Agent.ExecuteToolCallAsync` before the tool result is returned.
- Failed `stardew_submit_host_task` does not trigger the “missing todo only” self-check path.
- Successful valid move submission still queues one `stardew_host_task_submission` ingress with mechanical target and root todo id.
- Existing no-world-action and missing-delegation self-check behavior remains intact.
- Failed `npc_no_world_action` does not suppress delegation self-check.
