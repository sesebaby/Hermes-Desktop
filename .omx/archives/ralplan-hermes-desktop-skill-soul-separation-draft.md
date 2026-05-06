# Hermes Desktop Consensus Plan Draft: Skill/Soul Separation

## Plan Summary

**Plan saved to:** `.omx/plans/ralplan-hermes-desktop-skill-soul-separation-draft.md`

**Scope:**
- ~11 tasks across ~9-13 likely files
- Estimated complexity: HIGH

**Key Deliverables:**
1. Reference-aligned skill discovery that loads only `SKILL.md` skills and rejects legacy flat `.md` skills.
2. A separate soul distribution/runtime channel so souls are no longer bundled or discovered through the skills tree.

## Grounded repo facts

- [`src/skills/SkillManager.cs`](/abs/path/D:/Projects/Hermes-Desktop/src/skills/SkillManager.cs:36) currently enumerates `*.md` recursively and skips only `DESCRIPTION.md`.
- Soul templates under `skills/souls/*.md` are therefore treated as accidental skills.
- [`src/soul/SoulRegistry.cs`](/abs/path/D:/Projects/Hermes-Desktop/src/soul/SoulRegistry.cs:15) is already a separate subsystem and scans configured soul search paths independently of `SkillManager`.
- Desktop startup wires `SkillManager` to `%LOCALAPPDATA%/hermes/hermes-cs/skills` and wires `SoulRegistry` to:
  - [`AppContext.BaseDirectory/skills/souls`](</abs/path/D:/Projects/Hermes-Desktop/Desktop/HermesDesktop/App.xaml.cs:447>)
  - `%LOCALAPPDATA%/hermes/hermes-cs/souls`
  - `%LOCALAPPDATA%/hermes/hermes-agent/skills/souls`
- Bundled skill reconciliation/export already treats `SKILL.md` roots as canonical via [`src/skills/BundledSkillCatalogService.cs`](/abs/path/D:/Projects/Hermes-Desktop/src/skills/BundledSkillCatalogService.cs:437).
- Portable publish currently copies only the `skills/` tree into the publish output via [`scripts/publish-portable.ps1`](/abs/path/D:/Projects/Hermes-Desktop/scripts/publish-portable.ps1:102).
- Current skill-facing consumers include:
  - mandatory prompt assembly in [`SkillManager.BuildSkillsMandatoryPrompt()`](/abs/path/D:/Projects/Hermes-Desktop/src/skills/SkillManager.cs:167)
  - tool surfaces in [`src/Tools/SkillManagementTools.cs`](/abs/path/D:/Projects/Hermes-Desktop/src/Tools/SkillManagementTools.cs:19)
  - subagent/tool wiring in [`src/Tools/AgentTool.cs`](/abs/path/D:/Projects/Hermes-Desktop/src/Tools/AgentTool.cs:91)
  - memory review prompts/bridging in [`src/memory/MemoryReviewService.cs`](/abs/path/D:/Projects/Hermes-Desktop/src/memory/MemoryReviewService.cs:43)
  - chat slash help in [`Desktop/HermesDesktop/Views/ChatPage.xaml.cs`](/abs/path/D:/Projects/Hermes-Desktop/Desktop/HermesDesktop/Views/ChatPage.xaml.cs:475)
  - dashboard counts in [`Desktop/HermesDesktop/Views/DashboardPage.xaml.cs`](/abs/path/D:/Projects/Hermes-Desktop/Desktop/HermesDesktop/Views/DashboardPage.xaml.cs:143)
  - skills page/panel in [`Desktop/HermesDesktop/Views/SkillsPage.xaml.cs`](/abs/path/D:/Projects/Hermes-Desktop/Desktop/HermesDesktop/Views/SkillsPage.xaml.cs:29) and [`Desktop/HermesDesktop/Views/Panels/SkillsPanel.xaml.cs`](/abs/path/D:/Projects/Hermes-Desktop/Desktop/HermesDesktop/Views/Panels/SkillsPanel.xaml.cs:31)
- Reference repo behavior is aligned around `SKILL.md` discovery in [`external/hermes-agent-main/tools/skills_tool.py`](/abs/path/D:/Projects/Hermes-Desktop/external/hermes-agent-main/tools/skills_tool.py:546), while personality identity lives on `SOUL.md` / separate context paths in `external/hermes-agent-main`.

## RALPLAN-DR

### Principles

1. **One channel, one artifact type.** Skills are discovered only from `SKILL.md`; souls/personality are discovered only from soul-specific paths.
2. **Reference alignment over compatibility shims.** Match `external/hermes-agent-main` semantics directly instead of preserving flat `.md` legacy behavior.
3. **Regression safety before migration breadth.** Lock current non-soul runtime behavior with tests before changing discovery, startup, or packaging.
4. **Visible behavior stays coherent.** Any removed or relocated runtime surface must have matching UI/help/package updates in the same rollout.

### Decision drivers

1. **Correctness of discovery boundaries.** Fake skills must disappear without dropping legitimate skills.
2. **Reference parity.** Hermes Desktop should converge on the upstream `SKILL.md` vs soul split.
3. **Low-regression rollout.** Startup, package output, slash help, dashboard counts, and skills tooling must continue to work normally.

### Viable options

#### Option A: Minimal filter-only fix

Change `SkillManager` to discover only `SKILL.md`, leave shipped souls physically under `skills/souls`, and rely on `SoulRegistry` continuing to read them from there.

**Pros**
- Smallest code diff.
- Lowest immediate packaging/startup churn.
- Quickly removes accidental fake skills.

**Cons**
- Souls still physically live inside the skills bundle, which preserves the conceptual coupling the task wants removed.
- Does not fully align with reference separation.
- Keeps future packaging/startup ambiguity alive.

#### Option B: Full channel separation with dedicated shipped souls root

Change `SkillManager` to `SKILL.md`-only, relocate bundled soul templates out of `skills/` into a dedicated `souls/` publish/runtime root, and repoint startup/package paths accordingly.

**Pros**
- Fully satisfies all four task goals.
- Best matches reference architecture: skills and souls are loaded independently.
- Eliminates future accidental cross-discovery.

**Cons**
- Touches discovery, startup, package layout, and soul asset locations.
- Requires migration-aware verification for both packaged and local runtime paths.

#### Option C: Compatibility bridge with dual discovery

Support `SKILL.md`, flat `.md` skills, and current `skills/souls/*.md` during a transition period.

**Pros**
- Softest migration for unknown legacy content.

**Cons**
- Explicitly disallowed by the task.
- Preserves ambiguity and makes regressions harder to reason about.
- Moves Hermes Desktop farther from the reference model.

### Recommended option

**Choose Option B.**

It is the only option that satisfies:
- `SKILL.md`-only discovery
- soul separation from the skills system
- no flat `.md` compatibility
- reference alignment with `external/hermes-agent-main`

Option A is a valid temporary implementation slice, but not a sufficient end state. The rollout should still use Option A’s discovery-tightening as the first execution stage inside the broader Option B migration.

## Recommended implementation stages

### Stage 1: Lock discovery boundaries with characterization tests

Targets:
- `Desktop/HermesDesktop.Tests/Services/SkillSelfEvolutionParityTests.cs`
- new or expanded soul-registry tests
- possibly startup/package-focused tests where cheap

Work:
- Add tests proving `SkillManager` loads directory skills with `SKILL.md`.
- Add negative tests proving non-`SKILL.md` markdown files, including `skills/souls/*.md`, do not appear in `ListSkills()`.
- Add soul-registry tests proving soul discovery continues independently from its configured paths.

Acceptance criteria:
- There is a failing test before implementation for accidental soul-as-skill discovery.
- Existing skill-management behavior for real `SKILL.md` skills remains covered.
- Soul loading has direct test coverage independent of skill discovery.

### Stage 2: Tighten `SkillManager` to `SKILL.md`-only and remove flat `.md` compatibility

Targets:
- `src/skills/SkillManager.cs`
- any tests or helpers coupled to old path assumptions

Work:
- Replace recursive `*.md` enumeration with `SKILL.md` root discovery.
- Ensure category description loading still works.
- Preserve supporting-file access for directory-based skills only.
- Remove any code paths that imply flat-file skills are valid.

Acceptance criteria:
- Only directories containing `SKILL.md` are listed as skills.
- `skills_list`, `skill_view`, `skill_invoke`, mandatory prompt generation, `MemoryReviewService`, and `AgentTool` still operate for valid skills.
- No fallback path exists for legacy flat `.md` skills.

### Stage 3: Separate shipped soul assets from the skills tree

Targets:
- bundled repo assets under `skills/souls/*`
- new dedicated repo soul asset location, likely `souls/` or equivalent dedicated channel
- any source-control manifests that assume souls are part of bundled skills

Work:
- Move bundled soul templates out of `skills/`.
- Choose a stable dedicated package/runtime layout for shipped soul templates.
- Keep user-installed soul path under `%LOCALAPPDATA%/hermes/hermes-cs/souls`.

Acceptance criteria:
- Bundled souls are no longer stored under the repo’s `skills/` tree.
- Skill manifests/export logic no longer need to account for soul content.
- Local user soul installation location remains valid.

### Stage 4: Rewire startup and packaging to the new soul channel

Targets:
- `Desktop/HermesDesktop/App.xaml.cs`
- `scripts/publish-portable.ps1`
- any MSIX/publish scripts or packaging assets with parallel logic

Work:
- Update shipped soul search path from `AppContext.BaseDirectory/skills/souls` to the new dedicated publish root.
- Keep existing user and Hermes CLI soul search paths unless a stronger reference-aligned migration is required.
- Update publish scripts to bundle both `skills/` and the new `souls/` tree.

Acceptance criteria:
- Portable output contains both valid skills and shipped souls in their new locations.
- Fresh packaged startup loads skills and souls successfully.
- Startup no longer depends on `skills/souls`.

### Stage 5: Align UI/help text and visible counts

Targets:
- `Desktop/HermesDesktop/Views/ChatPage.xaml.cs`
- `Desktop/HermesDesktop/Views/DashboardPage.xaml.cs`
- `Desktop/HermesDesktop/Views/SkillsPage.xaml.cs`
- `Desktop/HermesDesktop/Views/Panels/SkillsPanel.xaml.cs`
- any soul-browser UI if present

Work:
- Ensure skills counts drop only by removing fake soul entries.
- Update user-facing text that still tells users to add arbitrary `.md` files to the skills directory.
- If a separate soul browser or entry exists, ensure its copy reflects the dedicated soul channel.

Acceptance criteria:
- `/help` and `/skills` text no longer advertises generic `.md` skill installation.
- dashboard and skills UI reflect true skill counts only.
- no UI element suggests souls live under the skills system.

### Stage 6: End-to-end regression sweep and package validation

Targets:
- unit/integration/manual/package verification surfaces

Work:
- Run the full verification matrix below.
- Compare pre/post skill inventories and soul inventories.
- Validate that non-skill features remain intact through standard runtime smoke coverage.

Acceptance criteria:
- Normal skill runtime works for bundled and user-installed `SKILL.md` skills.
- Soul templates remain discoverable through the soul subsystem.
- No regressions in subagent orchestration, Dreamer, todo/cron, memory/soul/profile, MCP, or media/voice/image feature startup.

## Safest first implementation step

**Add characterization tests before changing discovery code.**

Why this is safest:
- It isolates the highest-risk semantic change first: what counts as a skill.
- It creates a regression harness for every downstream consumer that depends on `SkillManager.ListSkills()`.
- It allows the later asset move and packaging change to proceed with confidence that any count/discovery regression is real and localizable.

Concrete first step:
- Extend `SkillSelfEvolutionParityTests` with a fixture containing:
  - one real directory skill with `SKILL.md`
  - one flat `legacy.md`
  - one `souls/default.md`
- Assert that only the real directory skill is surfaced by `SkillManager`.

## Premortem

1. **Packaged app loses shipped souls after relocation.**
   - Cause: publish script or startup path updated on only one side.
   - Mitigation: package-level test that inspects publish output and runtime-level test that `SoulRegistry.ListSouls()` is non-empty on fresh portable output.

2. **Real skills disappear because some local/bundled content is still flat `.md`.**
   - Cause: undiscovered legacy skill assets outside the known `skills/souls` bucket.
   - Mitigation: inventory scan before the code change, explicit test fixtures for retained bundled skills, and planned rejection of any remaining flat files as migration work rather than implicit support.

3. **UI/help copy and counts become misleading after the backend fix.**
   - Cause: skills count drops are correct, but visible text still says “add .md files” or assumes soul files are skills.
   - Mitigation: pair backend discovery change with chat/help/dashboard/skills UI text review in the same rollout.

## Expanded verification plan

### Unit

- `SkillManager`:
  - loads directory skills from `SKILL.md`
  - ignores non-`SKILL.md` markdown files
  - preserves supporting-file access for directory skills
  - builds mandatory prompt from true skills only
- `SoulRegistry`:
  - loads soul templates from dedicated soul paths
  - remains independent from skill discovery results
- `BundledSkillCatalogService`:
  - retained skill export/reconcile still works with `SKILL.md` roots only

### Integration

- `skills_list`, `skill_view`, `skill_invoke` against a temp skills directory with mixed valid/invalid files.
- `MemoryReviewService` flow that calls `skills_list`/`skill_view` and still sees valid skills.
- `AgentTool` subagent tool availability remains unchanged except for corrected skill inventory.
- `PromptBuilder` mandatory skills prompt still renders and excludes souls.

### Manual

- Launch desktop dev build and confirm:
  - skills page shows only real skills
  - skills panel preview still works
  - dashboard skill count reflects only real skills
  - `/help` and `/skills` list only true skills
  - soul browsing/apply flow still shows bundled souls
- Verify representative non-skill runtime flows:
  - subagent orchestration
  - Dreamer startup
  - todo/cron entry
  - memory review nudge path
  - MCP initialization
  - retained media/voice/image entry points

### Package

- Run portable publish and inspect output for:
  - `skills/` with only bundled skill assets
  - dedicated `souls/` shipped alongside
- Launch the portable output on a clean profile and confirm skills and souls both load.

### Runtime / smoke

- Fresh startup with empty `%LOCALAPPDATA%/hermes/hermes-cs/skills` and `%LOCALAPPDATA%/hermes/hermes-cs/souls`.
- Startup with existing user skills and existing user souls.
- Startup with Hermes CLI soul directory present at `%LOCALAPPDATA%/hermes/hermes-agent/skills/souls`.

## ADR

### Decision

Adopt strict `SKILL.md`-only skill discovery and move bundled souls/personality assets onto a separate soul distribution/runtime channel outside the `skills/` tree.

### Drivers

- Reference alignment with `external/hermes-agent-main`
- Eliminate accidental fake skills from soul markdown
- Preserve normal runtime behavior through a staged, test-first migration

### Alternatives considered

- **Filter-only backend fix:** good first slice, but insufficient final architecture because souls still live inside `skills/`.
- **Dual compatibility for flat `.md` skills:** explicitly rejected by task constraints and increases long-tail ambiguity.

### Why chosen

This is the only option that fully satisfies the required end state without leaving a hidden coupling between skill packaging and soul packaging.

### Consequences

- Skill counts will decrease where souls were previously miscounted as skills.
- Packaging must now ship two distinct content trees.
- Any truly legacy flat `.md` skills will stop loading and must be migrated explicitly.

### Follow-ups

- Audit the repo and user docs for any remaining “drop a `.md` file in skills” guidance.
- Consider adding a one-time diagnostic/warning for orphaned flat `.md` files discovered under user skills directories.
- Decide whether future soul assets should standardize on directory-based `SOUL.md` packaging for even closer upstream parity.

## Consensus mode

- RALPLAN-DR included:
  - Principles: yes
  - Drivers: yes
  - Options: yes
  - Premortem: yes
  - Expanded verification: yes
- ADR included:
  - Decision: yes
  - Drivers: yes
  - Alternatives considered: yes
  - Why chosen: yes
  - Consequences: yes
  - Follow-ups: yes

## Does this plan capture your intent?

- `proceed` - I’ll turn this draft into executable next-step commands / handoff guidance
- `adjust [X]` - I’ll revise the draft around scope, migration shape, or verification depth
- `restart` - I’ll discard this draft and re-plan from scratch
