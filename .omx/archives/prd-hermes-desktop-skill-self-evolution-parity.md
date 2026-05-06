# PRD: Hermes Desktop Python Skill Self-Evolution Parity

## Goal
Align the C# agent's self-evolution loop with Python `external/hermes-agent-main` for skills as procedural memory.

## Reference Files
- `external/hermes-agent-main/agent/prompt_builder.py`
- `external/hermes-agent-main/tools/skill_manager_tool.py`
- `external/hermes-agent-main/run_agent.py`
- `external/hermes-agent-main/model_tools.py`
- `external/hermes-agent-main/cli-config.yaml.example`

## Requirements
1. Expose Python-style skill tools to the model: `skills_list`, `skill_view`, and `skill_manage`.
2. `skill_manage` must support `create`, `patch`, `edit`, `delete`, `write_file`, and `remove_file` with Python-compatible parameter names.
3. Supporting file writes/removes must be confined to skill-owned `references/`, `templates/`, `scripts/`, or `assets/` paths.
4. Add model-facing `SKILLS_GUIDANCE` and inject it only when `skill_manage` is available.
5. Track skill nudge pressure by tool-call iterations, not user turns, matching Python's `_iters_since_skill` semantics.
6. Extend background review so it can review memory, skills, or both after the user-visible response is complete.
7. Skill review must first survey existing skills (`skills_list`, then `skill_view`) and prefer patching an existing skill before creating a new one.
8. Skill review must not mutate the foreground conversation history or block user response delivery.
9. Keep current C# memory/session_search parity behavior intact.

## Non-Goals
- Implement external memory providers like Honcho.
- Treat `DreamerService` as Python self-evolution parity.
- Add new package dependencies.

## Acceptance Criteria
- Agent tool definitions include `skills_list`, `skill_view`, and `skill_manage` with Python-compatible action enum and required fields.
- System prompt builder supports and tests skill guidance injection.
- Desktop startup registers skill management tools and enables skill guidance.
- Background review can execute both memory and skill tool calls.
- Skill nudge counter triggers by tool iterations and resets when `skill_manage` is used.
- Unit tests cover schema, tool execution, prompt guidance, nudge trigger, and background review behavior.
