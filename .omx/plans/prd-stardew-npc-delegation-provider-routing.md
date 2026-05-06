# PRD: Stardew NPC Delegation Provider Routing

## Goal

Enable Stardew NPC runtime to route autonomy, private chat, and delegated sub-agent work to different configured LLM lanes so high-frequency middle work can run locally or cheaply while player-visible dialogue can remain on cloud models.

## Users

- Player: should not pay cloud-model costs for repeated middle-step world checks and movement/action reasoning.
- Project owner: should configure local/cloud routing in `config.yaml` without code edits.
- Developer/agent: should verify lane choice through tests and logs.

## Requirements

1. Add config-driven route resolution for these lanes:
   - `model` as root parent.
   - `stardew_autonomy`.
   - `stardew_private_chat`.
   - `delegation`.
2. A lane may override `provider`, `model`, `base_url`, `api_key`, `api_key_env`, `auth_mode`, `auth_header`, `auth_scheme`, `auth_token_env`, and `auth_token_command`.
3. Missing lane fields inherit from the root `model:` section.
4. LM Studio local endpoint is a recommended config value: `http://127.0.0.1:1234/v1`; it must not be required or hardcoded as the only value.
5. Stardew autonomy uses the autonomy lane.
6. Stardew private chat uses the private-chat lane.
7. The NPC runtime `agent` tool uses the delegation lane.
8. Delegation v1 is single-child and flat-only.
9. `max_concurrent_children` may be parsed/logged as reserved but must not be presented as implemented batch scheduling.
10. OpenAI-compatible structured streaming must preserve provided `systemPrompt` and `tools`.
11. Logs must identify the lane and selected provider/model without exposing secrets.

## Non-Goals

- Settings UI for these lanes.
- Batch delegation.
- Nested delegation.
- Replacing `AgentTool` with `AgentService`.
- New NuGet dependencies.
- New Stardew-specific agent roles in this slice.

## Acceptance Criteria

1. Tests prove lane config inheritance and override precedence.
2. Tests prove autonomy/private_chat/delegation can select different fake clients.
3. Tests prove `AgentTool` uses delegation client when supplied.
4. Tests prove `OpenAiClient.StreamAsync` includes `systemPrompt` and `tools` in payload.
5. Existing NPC runtime tests still pass.
6. No secret values are logged.

## Rollout

First land config-only routing and tests. Manual LM Studio smoke follows after unit tests pass.
