# Hermes Desktop Skill Content Cleanup: Execution-Ready RALPLAN Draft

## Scope
- Remove all user-approved `Delete approved` bundled skill content.
- Keep `Retain approved` and `Defer approved` skills.
- Preserve skill framework, AgentTool/subagent orchestration, Dreamer, todo/cron, memory/soul/profile, MCP, and media/voice/vision capabilities.
- Ensure deleted skills disappear from UI and runtime, not just from visibility.

## RALPLAN-DR

### Principles
- One source of truth: bundled skill content and user-visible skill inventory must converge to the same approved set.
- No dual-track: if a skill is deleted, it must be removed from disk and from all dynamic surfaces that enumerate skills.
- Preserve runtime foundations: do not touch the framework/tools that make Hermes operate normally.
- Verify by enumeration, not assumption: prove the surviving skill set in repo, portable output, and old-user state.

### Decision Drivers
- `SkillManager` loads every `.md` under `skills/` and UI surfaces bind directly to `ListSkills()`.
- `App.xaml.cs` only seeds `projectDir/skills` when it is missing or empty, so old users retain stale bundled skills unless cleaned.
- `publish-portable.ps1` packages repo `skills/` into the portable build, so repo cleanup must match runtime cleanup.
- The manual review already assigns 69 delete / 13 retain / 8 defer across 90 runtime-visible skills.

### Viable Options
- Hard delete from repo + reconcile old user `projectDir/skills` against the approved set. Chosen.
- Delete only from repo and rely on first-run copy. Rejected: leaves old users on a different skill set.
- Hide deleted skills in UI only. Rejected: violates the no-dual-track requirement and leaves runtime loadable content behind.

## ADR

### Decision
Proceed with a destructive-by-name cleanup of bundled skills, paired with an explicit startup/user-directory reconciliation pass and UI verification.

### Drivers
- Dynamic discovery means UI deletion follows disk deletion only if the local skill inventory is reconciled.
- Portable packaging means repo state is shipping state.
- Preserved capabilities are already protected by the approval matrix.

### Alternatives Considered
- Keep old user copies untouched.
- Introduce a runtime filter layer.
- Rename deleted skills instead of removing them.

### Consequences
- Old installations will lose bundled deleted skills after reconciliation.
- Custom user skills can remain if they do not collide with deleted bundled names.
- Any missed static reference would be a defect, not a supported fallback.

### Follow-ups
- Add a guard test that fails if a deleted skill name remains loadable from repo, portable output, or user-dir reconciliation.
- Add a guard test that fails if UI counts or lists still show deleted skills.

## Can This Be Done Without Breaking Normal Functionality?
Yes, with boundaries:
- Safe: chat, memory, todo/cron, Dreamer, soul/profile, MCP, AgentTool/subagents, and retained/deferred skill content remain available.
- Not safe to skip: old-user `projectDir/skills` cleanup and UI/runtimesurface reconciliation.
- Main residual risk: deleting a locally customized skill that collides by name with a deleted bundled skill.

## Execution Plan

1. Freeze the approved skill manifest.
- Materialize the 90 runtime-visible decisions into a canonical allow/delete/defer list.
- Treat the four duplicate names (`code-review`, `plan`, `systematic-debugging`, `test-driven-development`) as name-level duplicates, not separate survivors.
- Acceptance: the manifest matches the manual review exactly, with 69 delete / 13 retain / 8 defer.

2. Remove deleted bundled skills from the repo.
- Delete all `Delete approved` bundled skill directories/files under `skills/`.
- Remove any duplicate definition paths for the four duplicated names so no alternate path can survive by accident.
- Keep `Retain approved` and `Defer approved` content intact.
- Acceptance: repo `skills/` contains only retained/deferred bundled skills plus any non-skill support files required by the remaining framework.

3. Reconcile old user `projectDir/skills` on startup.
- Add a one-time, idempotent cleanup pass that deletes only bundled skill names on the approved delete list from `projectDir/skills`.
- Prune empty directories after removal.
- Preserve user-authored skills that do not collide with deleted bundled names.
- Acceptance: an old install no longer loads deleted skills, and retained/deferred skills still load normally.

4. Ensure UI exposure disappears with the data.
- Verify no page/panel/dashboard path keeps a static deleted-skill entry.
- Keep the current dynamic UI model, but ensure it reads only the reconciled runtime inventory.
- Acceptance: `SkillsPage`, `SkillsPanel`, `ChatPage`, and `DashboardPage` show only surviving skills/counts.

5. Verify the ship target.
- Check the portable publish output only contains the surviving bundled skill set.
- Run focused tests for skill loading, cleanup reconciliation, UI counts, and publish packaging.
- Acceptance: a clean build, a portable publish, and a simulated old-user startup all show the same surviving inventory.

## Architect Review Should Challenge
- Whether user-owned custom skills that share a deleted bundled name should be removed unconditionally.
- Whether the cleanup should run once or on every startup as an idempotent reconciliation.
- Whether any deferred skill should be excluded from the initial portable bundle even if it remains in source.
- Whether there are hidden non-UI references to deleted skill names in prompts, tests, docs, or startup nudges.
