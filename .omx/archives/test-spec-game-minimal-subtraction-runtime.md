# Test Spec: Game Minimal Subtraction Runtime

## Verification Targets

- Build `HermesDesktop.sln` or the scoped projects that compile the desktop app, core, agent, and tests.
- Run targeted tests for Dreamer config, panel helper logic, scheduling, and retained agent/session behavior where available.
- Scan source, tests, and Desktop UI files for removed type names and user-visible deleted feature labels.

## Required Scans

- Removed runtime symbols: GatewayService, SendMessageTool, platform adapters, execution backends, shell security analyzer, file/code tools, browser/web-search tools, and HomeAssistantTool.
- Removed UI surfaces: IntegrationsPage, FileBrowserPanel, Gateway/Platform settings, SearchProvider settings, Execution Environment settings, and Dreamer Discord channel fields.
- Retained symbols: AgentTool, AgentService, CoordinatorService, MailboxService, DreamerService, TodoWriteTool, ScheduleCronTool, TaskManager.

## Pass Criteria

- No compile errors introduced by deleted files.
- Removed features have no active UI navigation or settings entry.
- Resource files no longer contain stale labels that expose retired capabilities.
- Retained NPC-life foundations remain referenced and buildable.

## Known Gaps

- No game bridge behavior is tested in this phase because game integration is intentionally out of scope.
