# Reference Deviation Approvals: Desktop Todo Effect Parity

| Date | Capability | Drift layer | Reference behavior | Current / proposed desktop behavior | Why deviation may be necessary | Category | User approval | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 2026-04-30 | Session todo storage | Snapshot | One `TodoStore` instance per `AIAgent` / session. | Singleton `SessionTodoStore` keyed by desktop session id. | Desktop DI registers tools once for the app process; session-keyed store preserves the same authority boundary. | equivalent mapping | approved by plan if accepted | No implementation block; effect is equivalent. |
| 2026-04-30 | Hydration source | Authoritative Writeback | Fresh gateway agents scan conversation history for the latest `todo` tool response. | `TranscriptStore` observer and `HermesChatService.LoadSessionAsync` hydrate the projection from persisted tool messages. | Desktop has no TUI gateway agent-per-message lifecycle; transcript observer is the equivalent completion boundary. | equivalent mapping | approved by plan if accepted | No implementation block; effect is equivalent. |
| 2026-04-30 | Active task injection role | Prompt / Summary Assembly | Reference appends active todo snapshot as a `user` message after compression. | Desktop injects active todo snapshot as a `system` layer through `PromptBuilder`. | Desktop context assembly uses stable system/session layers and has no reference compression append path. | title-local adaptation | pending | Acceptable only if behavior remains active-only and does not reintroduce completed work. |
| 2026-04-30 | Visible todo UI | Player-visible Surface | TUI shows live todo panel with text glyphs and collapsible archived transcript trails. | WinUI side panel/history can use desktop-native labels, colors, layout, and controls. | Desktop app is WinUI, not Ink/TUI. User explicitly said pure UI differences can be exempt. | title-local adaptation | approved | UI substrate can differ, but state source, ordering semantics, active/incomplete meaning, and inspectability remain required. |
| 2026-04-30 | Panel ordering | Player-visible Surface | Todo list order is priority; TUI renders `todos.map` in stored order. | `SessionTaskPanelModel` currently sorts active items before completed/cancelled and in-progress before pending. | This may be a usability preference, but it changes priority-order semantics. | controlled deviation | pending | Must either preserve stored order or get explicit approval for active-first sorting. |
| 2026-04-30 | Turn-end todo archive | Player-visible Surface / Authoritative Writeback | At message completion, live todos are archived into transcript trail with completed collapsed or incomplete flagged. | Desktop should expose an inspectable derived task-history row/section rebuilt from persisted todo tool JSON; visual form may differ. | Desktop transcript UI may need a different rendering model, but same effect requires a user-visible/reviewable per-turn task snapshot. | controlled deviation | pending if omitted | Archive must be derived/read-only, idempotent, and never canonical. Omission still needs explicit approval. |
| 2026-04-30 | No-store error path | Projector / Executor | `todo_tool(store=None)` returns `{"error": "TodoStore not initialized"}`. | C# `TodoTool` requires a `SessionTodoStore` constructor dependency. | DI can make missing store unrepresentable. | title-local adaptation | pending | Add a construction/registration test only if this becomes risky. |

## Stop Rule

Implementation must not continue for rows marked `controlled deviation` with `pending`
approval if the chosen implementation keeps the deviation. To avoid waiting, the
implementation can instead remove the deviation by matching the reference behavior:

- preserve todo list order in the panel model
- add a desktop todo archive surface or equivalent inspectable task-history trail

## User Clarification

On 2026-04-30 the user approved exempting pure UI shape/style differences.
This covers literal TUI layout, glyphs, collapse widgets, colors, and exact visual
treatment. It does not exempt semantic differences: authority source, todo order
as priority, incomplete/completed meaning, active-only prompt reinjection, and an
inspectable task-history equivalent still need to match the reference effect
unless separately approved.
