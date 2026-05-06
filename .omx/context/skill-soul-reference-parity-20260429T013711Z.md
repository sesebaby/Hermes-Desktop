# Context Snapshot: Skill Soul Reference Parity

## Task Statement
Use `$ralplan` to produce a reference-aligned implementation plan for Hermes Desktop so the skill system matches `external/hermes-agent-main`: `SkillManager` should discover only `SKILL.md`, while personality/soul content should stay on its own channel. The plan must preserve normal runtime behavior.

## Desired Outcome
- `SkillManager` loads only real directory-based skills with `SKILL.md`.
- Soul templates are no longer exposed through the skill system.
- No legacy flat `.md` skill compatibility remains.
- Soul browsing, soul reset/apply flows, startup wiring, and packaged builds continue to work.
- Skill UI, slash help, and skill tools reflect only real skills.

## Known Facts / Evidence
- `src/skills/SkillManager.cs` currently scans `*.md` recursively and only excludes `DESCRIPTION.md`.
- The repo skill tree currently contains 21 `SKILL.md`, 8 `DESCRIPTION.md`, and 36 other markdown files.
- In the repo and active local user skill tree, the only non-`SKILL.md` markdown files with frontmatter names that can become fake skills are the soul templates under `skills/souls/*.md`.
- Skill-related consumers include `BuildSkillsMandatoryPrompt`, `skills_list`, `skill_view`, `skill_invoke`, chat slash help, dashboard skill count, skills page, skills panel, `MemoryReviewService`, and `AgentTool`.
- `SoulRegistry` scans its configured search paths for `*.md`.
- Desktop startup wires soul search paths to:
  - `AppContext.BaseDirectory/skills/souls`
  - `%LOCALAPPDATA%/hermes/hermes-cs/souls`
  - `%LOCALAPPDATA%/hermes/hermes-agent/skills/souls`
- Portable publish currently bundles only the `skills/` tree into the app output.
- Reference implementation scans skills by `SKILL.md` and loads the agent identity from `SOUL.md` separately.

## Constraints
- Align with `external/hermes-agent-main`.
- Do not break normal functionality during or after the change.
- No dual-track compatibility layer for legacy flat `.md` skills.
- Do not remove `AgentTool` / subagent orchestration.
- Do not remove Dreamer.
- Do not remove todo / cron.
- Do not remove soul/profile/memory foundations.
- Do not remove MCP or retained media/voice/image capabilities.
- If a removed runtime surface has a UI entry, the UI entry must also be updated or removed.

## Unknowns / Open Questions
- Which program-output path currently supplies the shipped soul templates during normal desktop runs.
- Whether any tests, docs, or release packaging assumptions still depend on the accidental `*.md` skill discovery behavior.
- Whether the safest rollout order is to fix soul packaging/pathing first or to narrow `SkillManager` first.

## Likely Touchpoints
- `src/skills/SkillManager.cs`
- `src/Tools/SkillManagementTools.cs`
- `src/Tools/SkillInvokeTool.cs`
- `src/memory/MemoryReviewService.cs`
- `src/Tools/AgentTool.cs`
- `src/soul/SoulRegistry.cs`
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop/Views/SkillsPage.xaml.cs`
- `Desktop/HermesDesktop/Views/Panels/SkillsPanel.xaml.cs`
- `Desktop/HermesDesktop/Views/ChatPage.xaml.cs`
- `Desktop/HermesDesktop/Views/DashboardPage.xaml.cs`
- `Desktop/HermesDesktop/Views/AgentPage.xaml.cs`
- `Desktop/HermesDesktop/Views/Panels/AgentPanel.xaml.cs`
- `scripts/publish-portable.ps1`
- `Desktop/HermesDesktop.Tests/Services/BundledSkillCatalogServiceTests.cs`
