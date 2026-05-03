# E-2026-0503-npc-autonomy-prompt-source-and-pause-gate

- id: E-2026-0503
- title: NPC autonomy prompt resources used a stale runtime copy and retried fatal resource pauses
- status: active
- updated_at: 2026-05-03
- keywords: [npc-autonomy, stardew, prompt-supplement, persona, rebind, pause]
- trigger_scope: [stardew, runtime, bugfix, review]

## Symptoms

- Updating persona pack prompt files after the runtime copy was seeded did not change the autonomy prompt or trigger an autonomy handle rebind.
- A missing required Stardew gaming skill paused the NPC for one iteration, but the next iteration could replace the resource error with `LlmConcurrencyLimit` and keep retrying the same fatal prompt-resource problem.
- After a required Stardew gaming skill became available again, an existing prompt-resource pause could keep short-circuiting forever before the prompt builder rechecked the resource.

## Root Cause

- The prompt supplement builder reused `NpcNamespace.SeedPersonaPack`, whose copy-if-missing behavior is correct for runtime namespace initialization but wrong as the latest prompt source of truth.
- The background worker stored prompt-resource failures as a pause reason, but had no early gate to honor that fatal pause before acquiring LLM capacity or rebuilding the prompt.
- The later early gate treated the stored prompt-resource pause as permanently fatal, so recovered resources were never re-evaluated.

## Bad Fix Paths

- Do not make `SeedPersonaPack` overwrite runtime files globally; runtime namespace initialization and prompt source freshness are separate contracts.
- Do not handle missing prompt resources through the ordinary restart cooldown path.
- Do not move autonomy decisions into host-side Stardew behavior branches to compensate for missing prompt guidance.

## Corrective Constraints

- Autonomy prompt supplements must read current persona pack files and required `skills/gaming/*.md` files, not stale runtime copies.
- Prompt supplement text must remain in the autonomy rebind key so prompt resource changes rebuild the handle.
- Fatal prompt-resource pauses must short-circuit later autonomy ticks before LLM slot acquisition while still allowing pending action/private-chat ingress handling.
- Prompt-resource pauses must re-run prompt resource resolution before preserving the pause, so a repaired skill/persona source can resume without manually resetting runtime state.

## Verification Evidence

- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewAutonomyTickDebugServiceTests.RunOneTickAsync_WhenPackPromptFilesChange_RebindsWithLatestPersonaAndRequiredSkills|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_MissingRequiredSkillPausesNpcWithoutLlmRetryLoop"`
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_WhenPromptResourcePauseBecomesResolvable_ResumesAutonomy|FullyQualifiedName~StardewNpcAutonomyBackgroundServiceTests.RunOneIterationAsync_MissingRequiredSkillPausesNpcWithoutLlmRetryLoop"`

## Related Files

- `src/games/stardew/StardewNpcAutonomyPromptSupplementBuilder.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewAutonomyTickDebugServiceTests.cs`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcAutonomyBackgroundServiceTests.cs`
