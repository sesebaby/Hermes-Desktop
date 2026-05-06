# Skill Manifest Reconcile Context

## Task
Implement the approved skill cleanup plan so deleted skills are removed/retired, deferred skills are non-shipping, retained skills remain available, and legacy user skill directories are reconciled safely.

## Desired Outcome
- Only retained skills ship and remain loadable.
- Deleted skills do not remain active or visible.
- Deferred skills are not shipped in this branch.
- Legacy bundled files are cleaned without name-only deletion.
- Ambiguous collisions are quarantined outside the active scan root.

## Known Facts
- `SkillManager` loads `*.md` recursively from the active skills tree.
- Startup now reconciles bundled skills into `projectDir/skills`.
- Portable publish now copies only manifest-retained skill roots.
- Tests exist for manifest counts, reconcile, and retained export.
- The repo manifest currently records 90 logical skills: 13 retain, 8 defer, 69 delete.

## Constraints
- Do not remove AgentTool/subagent orchestration.
- Do not remove Dreamer.
- Do not remove todo/cron.
- Do not remove memory/soul/profile.
- Do not remove MCP.
- Do not remove retained media/voice/vision capability.
- No dual-track behavior.
- Deleted content with UI entry points must disappear from UI/runtime.

## Unknowns
- Whether any docs still advertise the old skill count.
- Whether the manifest/reconcile implementation needs cleanup for clarity or safety.
- Whether any runtime path still leaks deleted/deferred skills into the active tree.

## Likely Touchpoints
- `src/skills/BundledSkillCatalogService.cs`
- `Desktop/HermesDesktop/App.xaml.cs`
- `scripts/publish-portable.ps1`
- `Desktop/HermesDesktop.Tests/Services/BundledSkillCatalogServiceTests.cs`
- `skills/.bundled-skills-manifest.json`

