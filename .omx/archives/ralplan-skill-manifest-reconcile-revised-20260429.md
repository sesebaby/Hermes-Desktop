# Revised RALPLAN: Skill Manifest Reconcile

## Requirements Summary

Fixed facts from repo inspection and user constraints:

- `SkillManager` recursively loads every `*.md` under the configured skills directory and later definitions overwrite earlier ones by `name` ([src/skills/SkillManager.cs](/D:/Projects/Hermes-Desktop/src/skills/SkillManager.cs:34), [src/skills/SkillManager.cs](/D:/Projects/Hermes-Desktop/src/skills/SkillManager.cs:39), [src/skills/SkillManager.cs](/D:/Projects/Hermes-Desktop/src/skills/SkillManager.cs:50)).
- Desktop currently seeds user skills only when `projectDir/skills` does not exist or is empty, by recursively copying the repo `skills/` tree ([Desktop/HermesDesktop/App.xaml.cs](/D:/Projects/Hermes-Desktop/Desktop/HermesDesktop/App.xaml.cs:362), [Desktop/HermesDesktop/App.xaml.cs](/D:/Projects/Hermes-Desktop/Desktop/HermesDesktop/App.xaml.cs:366), [Desktop/HermesDesktop/App.xaml.cs](/D:/Projects/Hermes-Desktop/Desktop/HermesDesktop/App.xaml.cs:916)).
- Portable publish currently copies the entire repo `skills/` tree into the publish output ([scripts/publish-portable.ps1](/D:/Projects/Hermes-Desktop/scripts/publish-portable.ps1:115)).
- UI/runtime surfaces enumerate `SkillManager.ListSkills()` directly, including `SkillsPage`, `SkillsPanel`, `/skills`, and dashboard skill count ([Desktop/HermesDesktop/Views/SkillsPage.xaml.cs](/D:/Projects/Hermes-Desktop/Desktop/HermesDesktop/Views/SkillsPage.xaml.cs:29), [Desktop/HermesDesktop/Views/Panels/SkillsPanel.xaml.cs](/D:/Projects/Hermes-Desktop/Desktop/HermesDesktop/Views/Panels/SkillsPanel.xaml.cs:32), [Desktop/HermesDesktop/Views/ChatPage.xaml.cs](/D:/Projects/Hermes-Desktop/Desktop/HermesDesktop/Views/ChatPage.xaml.cs:477), [Desktop/HermesDesktop/Views/DashboardPage.xaml.cs](/D:/Projects/Hermes-Desktop/Desktop/HermesDesktop/Views/DashboardPage.xaml.cs:145)).
- Repo has 4 duplicate-name skills today: `code-review`, `plan`, `systematic-debugging`, `test-driven-development`, each from two source paths under `skills/claude-code/...` and `skills/software-development/...`.
- `SkillsHub` already uses a quarantine concept for remotely installed skills, but bundled skill seeding has no authoritative manifest/provenance layer yet ([src/skills/SkillsHub.cs](/D:/Projects/Hermes-Desktop/src/skills/SkillsHub.cs:25), [src/skills/SkillsHub.cs](/D:/Projects/Hermes-Desktop/src/skills/SkillsHub.cs:104)).
- User hard constraints:
  - no dual-track behavior
  - deleted skills must disappear from both UI and runtime
  - do not remove AgentTool/subagent orchestration, Dreamer, todo/cron, memory/soul/profile, MCP, or retained media/voice/vision capability
  - do not clean legacy user directories by skill name alone
- 90 runtime-unique skills already have a manual disposition decision.

## RALPLAN-DR Summary

### Principles

1. One authoritative shipping set: runtime-visible skills come only from an explicit manifest, never from raw recursive copying.
2. Provenance before deletion: legacy/user disk state must be classified by managed provenance and content identity, not by skill name alone.
3. Quarantine over silent loss: anything removed from the active skill tree due to migration uncertainty is moved to quarantine with an audit trail first.
4. Defer means non-shipping: deferred skills are excluded from seed/publish/runtime, not “retained but hidden”.
5. Disk-first verification: approval requires assertions on actual files in repo seed output, user skill dir, quarantine, and portable publish output.

### Decision Drivers

1. Current behavior is path-recursive and name-overwriting, so UI/runtime correctness follows disk state, not policy intent.
2. First-run-only seeding and portable full-tree copy currently create divergence between new installs, old installs, and published artifacts.
3. The user explicitly rejected name-only cleanup and any dual-track state where deleted skills still exist on disk or remain loadable.

### Viable Options

1. Manifest-driven reconcile with provenance ledger and quarantine-backed migration. Chosen.
   - Pros: satisfies no-dual-track, avoids name-only deletion, gives one shipping set for fresh installs, old installs, and portable builds.
   - Cons: requires a new manifest format, migration logic, and publish/startup changes.
2. Hard-delete repo skills plus startup name-based pruning in user dir.
   - Pros: smaller implementation.
   - Cons: explicitly rejected by requirement 1; unsafe for legacy customizations; no provenance boundary.
3. Keep raw repo tree and add runtime/UI denylist filtering.
   - Pros: lowest code churn.
   - Cons: violates “delete means disappear from disk and runtime”; portable/user disk still retain deleted content; verification remains superficial.

## ADR

### Decision

Adopt an authoritative bundled-skill manifest as the only shipping source of truth, and make both startup seeding and portable publish consume that manifest through the same reconcile/export path. Introduce a bundled-skill provenance ledger plus quarantine-backed migration so legacy user directories are cleaned by managed provenance and file identity, not by skill name alone.

### Drivers

- Current startup and publish behavior copy the repo `skills/` tree wholesale.
- `SkillManager` loads whatever markdown files remain on disk and resolves duplicates by last-write-wins name overwrite.
- Deleted skills must disappear from active disk, UI, and runtime for both fresh and legacy installs.

### Alternatives Considered

- Repo-delete + name-based legacy cleanup.
  - Rejected because it still deletes legacy content by skill name and cannot distinguish bundled-managed files from user-authored collisions.
- Runtime denylist only.
  - Rejected because it leaves disk/package state inconsistent with visible/runtime state.
- Keep defer as shipped-but-hidden.
  - Rejected because it is semantically defer but behaviorally retain.

### Why Chosen

This is the smallest plan that simultaneously fixes fresh install seeding, old install reconcile, portable publish, duplicate-name bleed-through, and disk-level verification without relying on unsafe name deletion.

### Consequences

- A new manifest and provenance file become required shipping artifacts.
- Legacy unprovenanced colliding files may be quarantined out of the active skill set if they cannot be proven as safe managed survivors.
- Repo `skills/` becomes an input catalog, but the manifest-defined retained subset becomes the only shipped/runtime set.

### Follow-Ups

- Keep a promotion path: a deferred skill only returns by a separate manifest change from `defer` to `retain`.
- Add a maintenance check that fails CI if repo seed/export/runtime inventory diverges from the manifest.

## Safe-Execution Preconditions

This plan is safe to execute only if all of the following are true:

1. The 90 reviewed skills are materialized into a checked-in authoritative manifest with explicit disposition and canonical source path metadata.
2. Startup seeding and portable publish are both switched to the same manifest-driven export/reconcile path before any legacy cleanup runs.
3. Legacy cleanup only touches files that are proven bundled-managed by provenance or exact manifest-mapped identity; anything uncertain is quarantined out of the active tree instead of deleted by name.
4. Deferred skills are excluded from the shipping/export set entirely, so “defer” does not remain loadable.
5. Quarantine storage lives outside the `SkillManager` recursive scan root, or the loader explicitly excludes it from discovery.
6. Verification covers disk state before and after reconcile/publish, not only `ListSkills()` or UI.

If these preconditions are not met, execution is not safe because the system would fall back to the rejected behavior of deleting by name or shipping undeclared runtime content.

## Defer Recommendation

Recommended rule: treat all 8 `defer` skills as **non-shipping** in this branch.

- They stay as source artifacts only for future reconsideration.
- They are excluded from the authoritative ship manifest.
- They are not copied into fresh user skill dirs.
- They are removed from active legacy user skill dirs through the same provenance/quarantine reconcile used for deleted bundled skills.
- They are not included in portable publish output.

Promotion rule: if a deferred skill should come back later, that is a separate reviewed manifest change from `defer` to `retain`, not a hidden runtime exception.

## Implementation Steps

1. Introduce the authoritative bundled-skill manifest and disposition model.
   - Add a checked-in manifest artifact for all 90 runtime-unique skills.
   - Required fields per logical skill: `name`, `disposition` (`retain|defer|delete`), canonical repo source path, duplicate source paths if any, content hash, and bundled provenance marker.
   - For the 4 duplicate names, represent one logical name with both source paths; because all 4 duplicate names are outside the retained set, the manifest must explicitly mark every source path as non-shipping.
   - Expected touchpoints: new manifest file under the repo plus a parser/model in `src/skills`.

2. Replace raw recursive bundled copy with a manifest-driven export/reconcile service.
   - Add a dedicated service in `src/skills` that:
     - exports only `retain` skills into an active seed directory
     - writes a provenance ledger for bundled-managed installed files
     - can reconcile an existing `projectDir/skills` tree against the current manifest
   - Wire Desktop startup to call this service instead of `CopyDirectoryRecursive` in [Desktop/HermesDesktop/App.xaml.cs](/D:/Projects/Hermes-Desktop/Desktop/HermesDesktop/App.xaml.cs:362).
   - Keep `SkillManager` as the loader of the reconciled active skill tree; do not add a second runtime source.

3. Make provenance and quarantine first-class for legacy migration.
   - Persist a bundled provenance ledger in the user/project skill area, for example a hidden sidecar file under `projectDir/skills`.
   - Place quarantine outside the active `projectDir/skills` scan root, or explicitly exclude the quarantine subtree from `SkillManager` discovery so quarantined markdown cannot re-enter runtime.
   - Migration rules:
     - previously managed bundled files that are now `delete` or `defer` move out of the active tree
     - managed bundled files still `retain` stay or get refreshed according to manifest hash/version
     - unprovenanced or hash-mismatched colliders are moved to quarantine, not deleted in place
   - Reuse the quarantine concept already present in `SkillsHub`, but for local bundled migration rather than remote downloads.
   - Quarantine must carry enough audit metadata to restore or inspect what was removed.

4. Align portable publish with the same authoritative ship set.
   - Replace the raw `Copy-Item -Recurse -Force $bundledSkills $targetSkills` step in [scripts/publish-portable.ps1](/D:/Projects/Hermes-Desktop/scripts/publish-portable.ps1:115) with manifest-driven export of only `retain` skills.
   - Ensure portable output contains the active shipping skill set and, if needed, the manifest/provenance metadata required for first-run verification.
   - Do not publish `defer` or `delete` skills, even if their source artifacts remain in the repo catalog.

5. Add disk-down verification tests and migration scenarios.
   - Extend `Desktop/HermesDesktop.Tests` with tests that assert actual file trees, not just `SkillManager.ListSkills()`.
   - Minimum scenarios:
     - fresh install seed from empty `projectDir/skills`
     - legacy install with managed deleted skills
     - legacy install with managed deferred skills
     - legacy install with same-name modified/unprovenanced collisions
     - portable publish output inventory
     - duplicate-name elimination for the four known duplicates
   - Keep existing `SkillManager` tests, but add new tests around the new export/reconcile service and startup integration.

6. Add a narrow operational note for future skill lifecycle decisions.
   - Document the meaning of `retain`, `defer`, and `delete`.
   - Document that repo presence does not imply shipping presence; only the manifest does.
   - Document the recovery path from quarantine and the promotion path from `defer` to `retain`.

## Acceptance Criteria

1. Fresh install:
   - Given an empty `projectDir/skills`, startup produces an active skill tree containing exactly the manifest `retain` set and no `defer` or `delete` entries on disk.
   - `SkillManager.ListSkills()` count and names match the retained manifest set exactly.

2. Legacy install reconcile:
   - Given a preexisting `projectDir/skills` populated from the old blind-copy behavior, startup removes every manifest `delete` and `defer` bundled-managed skill from the active tree without using skill-name-only deletion logic.
   - Removed managed files are either deleted with provenance proof or moved into quarantine with an audit record.
   - After startup, deleted/deferred skills are absent from active disk, `/skills`, `SkillsPage`, `SkillsPanel`, and dashboard counts.

3. Collision safety:
   - Given a legacy same-name file that lacks bundled provenance or does not match the expected bundled hash, startup does not silently treat it as a bundled-managed survivor.
   - That file is quarantined out of the active runtime path, and the audit record explains why.

4. Portable publish:
   - Portable publish output contains only manifest-retained skills.
   - None of the 8 deferred skills and none of the 69 deleted skills appear anywhere under the published `skills` output tree.

5. Duplicate-name closure:
   - The four duplicate logical names cannot be loaded at runtime after reconcile/publish because none of their source paths appear in the active ship set.
   - A disk enumeration of the active user skill tree and the portable publish skill tree shows zero source files for those duplicate names.

6. Non-target capability preservation:
   - No changes remove or disable AgentTool/subagent orchestration, Dreamer, todo/cron, memory/soul/profile, MCP, or retained media/voice/vision capability.
   - Existing related tests still pass, and no new plan step depends on deleting those subsystems.

## Verification Plan

- Unit:
  - manifest parser/model
  - provenance classification
  - quarantine decision rules
  - export of retained set only
- Integration:
  - startup reconcile against temp legacy skill dirs
  - `SkillManager` loading from reconciled output
  - dashboard/page/panel counts after reconcile
- Publish/package:
  - run portable publish to a temp output and assert disk inventory under `publish-portable/skills`
- Disk assertions:
  - enumerate actual files under repo seed output, user skill dir, quarantine dir, and portable publish dir
  - compare against manifest by name and relative path

## Risks And Mitigations

- Risk: legacy bundled/user collisions are ambiguous before provenance exists.
  - Mitigation: quarantine ambiguous colliders instead of deleting or retaining them silently.
- Risk: deferred skills remain behaviorally shipped through old copy paths.
  - Mitigation: both startup seeding and portable publish must consume the same manifest-driven exporter before rollout.
- Risk: repo keeps non-shipping source artifacts and future maintainers assume they ship.
  - Mitigation: manifest-driven CI check and explicit lifecycle documentation.
- Risk: quarantined markdown gets reloaded because it still lives under the active scan root.
  - Mitigation: keep quarantine physically outside the `SkillManager` scan root or teach the loader to exclude it explicitly.

## Scope

- Estimated complexity: MEDIUM
- Primary code areas: `src/skills`, `Desktop/HermesDesktop/App.xaml.cs`, `scripts/publish-portable.ps1`, `Desktop/HermesDesktop.Tests`
- Expected file count: moderate; centered on one new manifest, one new reconcile/export service, wiring changes, and focused tests

## Does This Plan Capture Your Intent?

- `proceed` - Show executable next-step commands
- `adjust [X]` - Return to planning and revise the specific area
- `restart` - Discard and start fresh
