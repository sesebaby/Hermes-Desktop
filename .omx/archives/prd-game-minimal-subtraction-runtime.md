# PRD: Game Minimal Subtraction Runtime

## Requirements Summary

Retire the confirmed non-game surfaces from Hermes Desktop so the runtime is smaller and clearer for the future "one NPC, one agent" direction. This phase is subtraction only: no game bridge, mod DLL, event API, or replacement integration layer.

## Preserve

- AgentTool and subagent orchestration.
- AgentService, CoordinatorService, and MailboxService.
- Dreamer core, local room files, todo, cron, TaskManager, and scheduler.
- Memory, soul/profile, AskUser, MCP framework, skills framework.
- Media, voice, and vision.

## Remove

- Gateway and external messaging platform runtime.
- UI entries and settings for Telegram, Discord, Slack, Matrix, WhatsApp, Webhook, and gateway status.
- Developer/code execution tools, file browser UI, shell/terminal execution, browser/web search, and smart-home tool.
- Configuration/resource strings that still expose retired capabilities.

## Acceptance Criteria

- Deleted runtime code has no remaining DI registration or tool registration.
- Deleted UI pages/panels have no remaining navigation entries.
- User-visible strings do not advertise removed gateway, platform, execution, browser/search, file browser, or Dreamer Discord behavior.
- Dreamer digest behavior remains useful without Discord by writing local digest markdown files.
- Build and relevant tests pass or any remaining failure is explicitly explained.

## ADR

Decision: delete confirmed non-game surfaces and keep uncertain NPC-life foundations.

Drivers:
- No dual-track runtime.
- Future NPCs need local agent life, scheduling, memory, Dreamer, and orchestration more than generic desktop/dev tools.
- UI must not expose retired capabilities.

Alternatives considered:
- Hide retired features behind config. Rejected because the user explicitly disallowed dual-track behavior.
- Delete all unclear capabilities aggressively. Rejected because uncertain features may support future NPC life.

Consequences:
- Desktop loses generic platform gateway, terminal/file/browser/web-search/smart-home tooling.
- Dreamer digest becomes local-first.
- Any future game integration must be discussed in a later phase.

Follow-ups:
- Separate review for skills/MCP content that is user-exposed but not runtime-registered.
- Separate game boundary design phase after this subtraction pass.
