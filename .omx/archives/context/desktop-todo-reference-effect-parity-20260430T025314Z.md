# Desktop Todo Reference Effect Parity Context

## Task Statement

Compare the current desktop todo/task-loop mechanism with the reference project at
`external/hermes-agent-main`, correct the plan toward the same effect, and keep
the workflow in `$ralplan` consensus-planning mode before implementation.

## Desired Outcome

Produce a reference-first correction plan for the desktop session todo loop:

- preserve the reference authority chain first
- identify what is already equivalent
- identify what is only partial
- record any deviation that requires explicit approval before implementation

## Known Facts / Evidence

- Reference todo core lives in `external/hermes-agent-main/tools/todo_tool.py`.
- Reference TUI projection path lives in:
  - `external/hermes-agent-main/tui_gateway/server.py`
  - `external/hermes-agent-main/ui-tui/src/app/turnController.ts`
  - `external/hermes-agent-main/ui-tui/src/app/turnStore.ts`
  - `external/hermes-agent-main/ui-tui/src/components/todoPanel.tsx`
- Current desktop todo implementation lives in:
  - `src/tasks/SessionTodoStore.cs`
  - `src/tasks/SessionTaskProjectionService.cs`
  - `src/tasks/SessionTaskPanelModel.cs`
  - `src/Tools/TodoWriteTool.cs`
  - `src/Core/Agent.cs`
  - `src/Context/ContextManager.cs`
  - `src/Context/PromptBuilder.cs`
  - `src/Desktop/HermesChatService.cs`
  - `Desktop/HermesDesktop/Views/Panels/TaskPanel.xaml.cs`
- Current implementation already has focused tests for tool semantics,
  projection, context injection, panel model, and chat-service lifecycle.
- Latest known full desktop test run before this planning turn was green:
  `664 passed, 1 skipped`.
- Current dirty worktree includes `.omx/state/session.json` and an untracked
  docs comparison file; these are not part of this correction plan.

## Constraints

- Reference-first rule applies: reconstruct reference chain before changing code.
- `$ralplan` rule applies: run Planner -> Architect -> Critic review and stop at
  final plan unless execution is explicitly invoked.
- Keep the desktop scope; do not reintroduce the reference gateway/TUI runtime as
  production code.
- User requested small steps and timely commits for implementation, but this
  turn is still planning-only.
- User requested agents use `gpt-5.5` where possible.

## Unknowns / Open Questions

- Whether the user accepts desktop UI as a `title-local adaptation` rather than
  literal TUI trail rendering.
- Whether the desktop app should add a session-history todo archive equivalent,
  or whether the existing transcript tool-result history is sufficient for this
  phase.
- Whether `todo_write` should stay as an executable compatibility alias after
  parity hardening, or be removed once migration is complete.

## Likely Codebase Touchpoints

- `src/Tools/TodoWriteTool.cs`
- `src/tasks/SessionTodoStore.cs`
- `src/tasks/SessionTaskProjectionService.cs`
- `src/tasks/SessionTaskPanelModel.cs`
- `src/Core/Agent.cs`
- `src/Context/PromptBuilder.cs`
- `src/Context/ContextManager.cs`
- `src/Desktop/HermesChatService.cs`
- `Desktop/HermesDesktop/Views/Panels/TaskPanel.xaml`
- `Desktop/HermesDesktop/Views/Panels/TaskPanel.xaml.cs`
- `Desktop/HermesDesktop.Tests/Tools/TodoToolTests.cs`
- `Desktop/HermesDesktop.Tests/Services/SessionTaskProjectionServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Services/SessionTaskPanelModelTests.cs`
- `Desktop/HermesDesktop.Tests/Services/HermesChatServiceTaskLoopTests.cs`
- `Desktop/HermesDesktop.Tests/Services/TaskContextInjectionTests.cs`
