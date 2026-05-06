# Context Snapshot: Game Minimal Subtraction Runtime Implementation

## Task Statement
Use `$ralplan` to create an implementation plan, then begin implementing the approved subtraction plan for Hermes Desktop's game-oriented minimal runtime.

## Desired Outcome
Retire confirmed non-game capabilities by deleting code, registrations, startup wiring, UI entries, configuration surfaces, and exposed skill/content references. Preserve all NPC-life foundations.

## Known Facts / Evidence
- Spec: `.omc/specs/深度访谈-游戏化最小减法运行时.md`
- Prior spec commit: `4dae1e42 Constrain game-runtime subtraction around NPC life`
- Dirty state before implementation: only `.omx/state/session.json` modified and intentionally unrelated.
- Desktop central registration: `Desktop/HermesDesktop/App.xaml.cs`
- Confirmed delete candidates include gateway/platform messaging, developer/code tools, browser/web tools, smart-home tool, and non-game MCP/skills content.
- Confirmed retain list includes `AgentTool` / subagent orchestration, Dreamer core, todo/cron/task scheduler, memory, soul/profile, AskUser, MCP framework, skills framework, media/voice/vision.

## Constraints
- No long-term dual-track runtime.
- Deprecated code must be removed or retired, not hidden behind config/profile.
- If a retired capability has UI entries, remove those entries too.
- Do not delete `AgentTool`, `AgentService`, `CoordinatorService`, `MailboxService`, Dreamer core, todo/cron/task scheduling, memory/soul/profile/player interaction, MCP framework, skills framework, media/voice/vision.
- Current phase is subtraction only; do not design game bridge/mod/DLL/event API yet.
- If uncertain whether something supports future NPC life, mark defer and do not delete in this pass.

## Unknowns / Open Questions
- Exact full list of skills/MCP content to retire may need a separate pass because repo includes bundled, external, and reference-project content.
- Some docs and wiki references may be historical rather than runtime-exposed; delete/update only where they expose retired runtime behavior or would mislead current users.

## Likely Touchpoints
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop/MainWindow.xaml`
- `Desktop/HermesDesktop/MainWindow.xaml.cs`
- `Desktop/HermesDesktop/Views/IntegrationsPage.xaml`
- `Desktop/HermesDesktop/Views/IntegrationsPage.xaml.cs`
- `Desktop/HermesDesktop/Views/SettingsPage.xaml`
- `Desktop/HermesDesktop/Views/SettingsPage.xaml.cs`
- `Desktop/HermesDesktop/Views/DashboardPage.xaml`
- `Desktop/HermesDesktop/Views/DashboardPage.xaml.cs`
- `Desktop/HermesDesktop/Views/ChatPage.xaml`
- `Desktop/HermesDesktop/Views/Panels/FileBrowserPanel.*`
- `src/Tools/*`
- `src/gateway/**`
- `skills/**`
- `mcp.json` / MCP config surfaces if present
