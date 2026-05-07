# Desktop Developer Page Context Snapshot

## Task Statement

Add a developer page to the Hermes Desktop UI, clarified through `$deep-interview` before implementation.

## Desired Outcome

The desktop app should expose developer/debug workflows in a clearer place, especially around NPC runtime/private chat/local executor diagnosis, while preserving existing runtime architecture.

## Stated Solution

User asked to modify the desktop UI and add a developer page.

## Probable Intent Hypothesis

The current UI exposes NPC runtime information under `Agent > Runtime` and dashboard cards, but there is no obvious developer-oriented workspace for direct NPC diagnostics, logs, traces, private chat/runtime state, or manual debug actions. The user likely wants a first-class page for development/debugging rather than burying these controls in product-facing pages.

## Known Facts / Evidence

- `Desktop/HermesDesktop/MainWindow.xaml` uses a single `NavigationView` with pages for `dashboard`, `chat`, `agent`, `skills`, `memory`, `buddy`, and `settings`.
- `Desktop/HermesDesktop/MainWindow.xaml.cs` maps navigation tags to page types in `PageMap`.
- `Desktop/HermesDesktop/Views/AgentPage.xaml` already has an `Agent > Runtime` tab with NPC runtime list and debug buttons.
- `Desktop/HermesDesktop/Views/AgentPage.xaml.cs` already uses `NpcRuntimeWorkspaceService`, `StardewNpcDebugActionService`, and `StardewAutonomyTickDebugService`.
- `Desktop/HermesDesktop/App.xaml.cs` already registers NPC runtime, private chat runner, debug services, and Stardew autonomy services.
- `Desktop/HermesDesktop/Strings/en-us/Resources.resw` already localizes nav/page/debug strings.
- User constraint: fully reuse existing base setup; do not create a dual-track implementation.

## Constraints

- Brownfield WinUI 3 desktop app; follow `Desktop/HermesDesktop/AGENTS.md`.
- Reuse existing navigation, service registrations, resource strings, runtime services, logs, and diagnostics surfaces.
- No new dependencies unless explicitly justified later.
- Do not fork NPC runtime/private chat/local executor protocols or create a second runtime lane.
- Deep-interview mode must not implement directly; produce requirements artifacts and handoff options.

## Unknowns / Open Questions

- Whether the developer page should be a new left-nav page or a restricted/dev-mode section under Settings/Agent.
- Which workflows are in scope for first pass: NPC runtime diagnostics, direct NPC private chat input, local executor traces, logs, MCP/tools, model lane config, task/todo state, or general app diagnostics.
- Whether the page is intended for internal developer use only or visible to normal users.
- Whether moving existing debug controls out of `Agent > Runtime` is in scope, or the first pass should link/reuse them.
- What constitutes success for the first iteration.

## Decision-Boundary Unknowns

- Can OMX choose the exact page layout and grouping?
- Can OMX move existing debug buttons from Agent Runtime to Developer, or should it only duplicate/link existing actions?
- Can OMX expose raw trace/log paths and diagnostic text in UI?
- Can OMX add a direct NPC prompt/debug action if it reuses existing private chat/runtime services?

## Likely Codebase Touchpoints

- `Desktop/HermesDesktop/MainWindow.xaml`
- `Desktop/HermesDesktop/MainWindow.xaml.cs`
- `Desktop/HermesDesktop/Views/AgentPage.xaml`
- `Desktop/HermesDesktop/Views/AgentPage.xaml.cs`
- `Desktop/HermesDesktop/Views/DashboardPage.xaml`
- `Desktop/HermesDesktop/Services/NpcRuntimeWorkspaceService.cs`
- `Desktop/HermesDesktop/Strings/en-us/Resources.resw`
- Possibly new `Desktop/HermesDesktop/Views/DeveloperPage.xaml` and `.xaml.cs`

