# Desktop Task Closure Context Snapshot

## Task Statement

Analyze the task functionality in `external/hermes-agent-main`, compare it to the current Hermes Desktop task implementation, and plan a reference-first path to make the desktop task feature closed-loop and usable.

## Desired Outcome

Implement, after planning approval, a desktop-only task loop:

- user gives a complex task in the WinUI chat
- agent uses a task/todo tool to create and update ordered work items
- task state is persisted and recoverable within the active desktop session
- the right-side Tasks panel updates from actual tool results
- completed/incomplete task state is archived into the conversation/session history
- context compression/session reload can recover active tasks
- tests prove the loop from tool call to UI/task state

## Scope Constraint

Current project has intentionally removed gateway/messenger surfaces. `Desktop/HermesDesktop/AGENTS.md:32` says the gateway surface is retired and the desktop shell should focus on local agent life, memory, Dreamer, skills, and task coordination. Therefore gateway, Telegram, Discord, Slack, and TUI gateway code in the reference project are evidence for event shape only; they are not implementation targets.

## Reference Facts

- `external/hermes-agent-main/tools/todo_tool.py:3` defines the feature as an in-memory planning/task management tool.
- `external/hermes-agent-main/tools/todo_tool.py:21` allows statuses `pending`, `in_progress`, `completed`, and `cancelled`.
- `external/hermes-agent-main/tools/todo_tool.py:38` supports replace and merge writes.
- `external/hermes-agent-main/tools/todo_tool.py:90` formats active pending/in-progress todos for post-compression injection.
- `external/hermes-agent-main/tools/todo_tool.py:156` exposes one read/write entry point: omit `todos` to read, provide `todos` to write.
- `external/hermes-agent-main/tools/todo_tool.py:209` embeds usage guidance in the tool schema: use for complex tasks, return full list, one item in progress, mark completed immediately, cancel failed items and add revised work.
- `external/hermes-agent-main/run_agent.py:1579` creates one `TodoStore` per agent/session.
- `external/hermes-agent-main/run_agent.py:4426` hydrates the todo store from the most recent todo tool response in conversation history.
- `external/hermes-agent-main/run_agent.py:8275` and `external/hermes-agent-main/run_agent.py:8783` intercept `todo` as an agent-level tool rather than generic registry execution.
- `external/hermes-agent-main/tui_gateway/server.py:1043` treats tool completion, not tool start, as source of truth for todos because start args may be partial merge updates.
- `external/hermes-agent-main/ui-tui/src/app/turnController.ts:46` validates todo statuses before UI state update.
- `external/hermes-agent-main/ui-tui/src/app/turnController.ts:318` records todos from tool events into turn state.
- `external/hermes-agent-main/ui-tui/src/app/turnStore.ts:45` archives todos at turn end as a transcript trail, marking incomplete todos explicitly.
- `external/hermes-agent-main/ui-tui/src/components/appLayout.tsx:33` anchors the live todo panel to the latest user-message row.
- `external/hermes-agent-main/ui-tui/src/components/todoPanel.tsx:49` hides the panel when there are no todos and shows progress counts when present.
- `external/hermes-agent-main/tests/tools/test_todo_tool.py:8` covers write/read behavior.
- `external/hermes-agent-main/tests/tools/test_todo_tool.py:51` covers post-compression injection filtering.
- `external/hermes-agent-main/tests/tools/test_todo_tool.py:75` covers merge mode.
- `external/hermes-agent-main/tests/run_agent/test_run_agent.py:810` covers todo hydration from history.
- `external/hermes-agent-main/tests/run_agent/test_run_agent.py:1916` covers agent-level todo interception.

## Current Project Facts

- `src/Tools/TodoWriteTool.cs:13` exposes `todo_write`, not reference-compatible `todo`.
- `src/Tools/TodoWriteTool.cs:17` defaults to `InMemoryTodoStore`.
- `src/Tools/TodoWriteTool.cs:22` only writes; there is no read mode.
- `src/Tools/TodoWriteTool.cs:121` supports `Pending`, `InProgress`, and `Completed`, but no `Cancelled`.
- `src/tasks/TaskManager.cs:47` creates persisted `HermesTask` JSON files.
- `src/tasks/TaskManager.cs:86` updates metadata but not status directly.
- `src/tasks/TaskManager.cs:129` completes tasks.
- `src/tasks/TaskManager.cs:149` fails tasks.
- `Desktop/HermesDesktop/App.xaml.cs:431` registers `TaskManager` with the desktop service provider.
- `Desktop/HermesDesktop/App.xaml.cs:766` registers `TodoWriteTool` independently from `TaskManager`.
- `Desktop/HermesDesktop/Views/Panels/TaskPanel.xaml.cs:40` displays `_taskManager.GetOrderedTasks()`.
- `Desktop/HermesDesktop/Views/Panels/TaskPanel.xaml.cs:64` only refreshes via explicit refresh click or initial load.
- `Desktop/HermesDesktop/Views/ChatPage.xaml.cs:96` already listens to `Agent.ActivityEntryAdded` and feeds the Replay panel, but this is summary/replay telemetry, not an authoritative task-state seam.
- `src/Core/Agent.cs` persists tool result messages with full `Message.Content`, `ToolName`, and `ToolCallId`; this is the stronger source for task projection than activity summaries.
- `src/transcript/TranscriptStore.cs` exposes an `ITranscriptMessageObserver` seam with `sessionId` and full messages, which can feed a desktop-only session task projector.
- `Desktop/HermesDesktop/Services/HermesChatService.cs:117` streams structured chat events to the WinUI page but currently only token/thinking/error are exposed.
- `src/Core/Agent.cs:920` streams a textual tool-call status marker.
- `src/Core/Agent.cs:936` executes tool calls and `src/Core/Agent.cs:983` appends tool result messages to the session.

## Known Gaps

- Ordinary chat does not write TaskPanel-visible tasks because the agent uses `todo_write`, while TaskPanel reads `TaskManager`.
- Current `todo_write` has no read mode, no merge mode, no cancelled status, and no full-list JSON contract matching the reference.
- Current TaskPanel is a static persisted task list, not a live turn/task panel.
- Tool completion results are not parsed into task UI state.
- Task state is not archived into transcript trail messages.
- No current tests directly cover `TaskManager`, `TaskPanel`, `TodoWriteTool`, or the tool-result-to-UI task loop.
- Existing `CoordinatorService` creates `HermesTask` items, but no desktop chat entry point calls `RunCoordinatedTaskAsync` or `RunBriefAsync`; it should not be the first dependency for closing the ordinary chat task loop.

## Reference Chain Reconstruction

1. Trigger: model calls `todo` for complex tasks or multi-task requests.
2. Snapshot: tool receives full or partial todo list and updates per-session `TodoStore`.
3. Prompt / Summary Assembly: tool schema drives behavior; context compressor can re-inject active task list.
4. Parse / Repair / Normalize: `TodoStore` validates ids/content/status and deduplicates by id; TUI validates incoming todo event payloads.
5. Projector / Executor: agent intercepts `todo` directly and returns full current list as tool result JSON.
6. Authoritative Writeback: tool completion result is source of truth; history hydration recovers state from last todo tool result.
7. Player-visible Surface: live TodoPanel displays current todos during the turn and archives a transcript trail at turn end.

## Current Project Mapping Target

- Trigger maps to `SystemPrompts.Default` plus a reference-compatible desktop task tool schema.
- Snapshot maps to a session-aware task store owned by desktop Agent/HermesChatService, optionally backed by `TaskManager` or a new session task snapshot store.
- Prompt / Summary Assembly maps to tool schema and future context/session replay injection.
- Parse / Repair / Normalize maps to a C# `TodoTaskStore` or revised `TodoWriteTool` validation layer.
- Projector / Executor maps to `Agent.ExecuteToolCallAsync` plus persisted task tool result messages.
- Authoritative Writeback maps to persisted session messages and a recoverable last-task snapshot owned by a session task projection service.
- Player-visible Surface maps to WinUI `TaskPanel`, refreshed from typed session task projection events, not by scraping activity summaries or assistant text.

## Open Questions

- Should persisted `TaskManager` represent long-lived project tasks, while session todos represent per-turn work items, or should both share one model?
- Should the public tool name stay `todo_write` for compatibility with existing prompts/tests, or add a reference-compatible `todo` alias?
- Should TaskPanel show only live session task state, or both live session task state and persisted coordinator/project tasks with filters?

## Initial Recommendation

Prefer a two-tier model:

- session task list: reference-faithful, ordinary chat loop, live UI, transcript recovery
- persisted `HermesTask`: longer-lived project/coordinator tasks, linked later once the ordinary loop is stable

This preserves the reference semantics without forcing every model planning item to become a durable project task.
