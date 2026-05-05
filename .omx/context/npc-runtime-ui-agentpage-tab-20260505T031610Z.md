# NPC Runtime UI AgentPage Runtime Tab

## Task statement

Execute the approved UI information architecture plan for the Stardew NPC runtime debugger in Hermes Desktop.

## Desired outcome

- Move the complete NPC Runtime workspace from `DashboardPage` into a new `Runtime` tab on `AgentPage`.
- Do not create a separate navigation page.
- Keep `DashboardPage` as a read-only summary card with runtime count, bridge health, last trace, last error, logs, and an entry button that opens the Agent Runtime tab.
- Complete English and Simplified Chinese resource coverage in the same change.

## Known facts/evidence

- `DashboardPage.xaml` currently owns the full runtime list and Haley/Penny/Haley tick debug controls.
- `DashboardPage.xaml.cs` currently owns `NpcRuntimeWorkspaceService`, `StardewNpcDebugActionService`, and `StardewAutonomyTickDebugService` handlers.
- `AgentPage.xaml` currently has three tabs: `agents`, `identity`, and `souls`.
- `AgentPage` uses `NavigationCacheMode="Required"`, so navigation parameters must be consumed in `OnNavigatedTo` or an equivalent cached-page-safe path.
- `MainWindow.NavigateToTag(string)` currently does not support navigation parameters.
- Existing runtime data should continue to come from `NpcRuntimeWorkspaceService` and its snapshot/item models.

## Constraints

- Agent Runtime must be the only full runtime workbench.
- Dashboard must not keep `NpcRuntimeList`, Haley/Penny speak buttons, Haley tick button, or manual action result area.
- Pages must not depend directly on `NpcRuntimeSupervisor`.
- Navigation from Dashboard must go through the shell so `NavigationView.SelectedItem` remains synchronized.
- User-facing strings must be resource-backed and present in both `Strings/en-us/Resources.resw` and `Strings/zh-cn/Resources.resw`.
- Build/test verification must run sequentially because parallel `dotnet build/test` can lock shared `obj` files.
- Do not revert unrelated dirty worktree changes.

## Unknowns/open questions

- Exact current resource key coverage must be confirmed before editing.
- Whether existing Agent page hardcoded strings are in scope for full localization or only new/touched Runtime UI strings; this execution will resource all newly added/changed Runtime UI strings and keep unrelated legacy text unchanged unless touched for tab wiring.

## Likely codebase touchpoints

- `Desktop/HermesDesktop/MainWindow.xaml`
- `Desktop/HermesDesktop/MainWindow.xaml.cs`
- `Desktop/HermesDesktop/Views/DashboardPage.xaml`
- `Desktop/HermesDesktop/Views/DashboardPage.xaml.cs`
- `Desktop/HermesDesktop/Views/AgentPage.xaml`
- `Desktop/HermesDesktop/Views/AgentPage.xaml.cs`
- `Desktop/HermesDesktop/Strings/en-us/Resources.resw`
- `Desktop/HermesDesktop/Strings/zh-cn/Resources.resw`
