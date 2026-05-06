# Context Snapshot: Skill Content Cleanup

## Task Statement
The screenshot shows the Desktop Skills page still displaying broad, generic bundled skills such as Apple Notes, arXiv, ASCII art, training, research, and other non-game capabilities. Re-run `$ralplan` for this residual surface and decide what needs to be removed cleanly.

## Desired Outcome
Keep the skills framework available for NPC/game runtime use, but remove bundled skill content and UI exposure that still presents Hermes Desktop as a general desktop/dev/research/productivity assistant.

## Known Facts / Evidence
- `Desktop/HermesDesktop/Views/SkillsPage.xaml.cs` lists `SkillManager.ListSkills()` and shows every installed skill.
- `Desktop/HermesDesktop/App.xaml.cs` copies the repo `skills/` directory into `<HermesHome>/hermes-cs/skills` on first run when the user skill directory is empty.
- The repo `skills/` directory still contains broad categories: `apple`, `claude-code`, `creative`, `github`, `mlops`, `productivity`, `research`, `smart-home`, `social-media`, and similar general assistant material.
- The spec explicitly says the skills framework must remain, but non-game skill entries and trigger content should be retired.
- Previous implementation commit preserved `SkillManager`, `skill_invoke`, `skills_list`, `skill_view`, and `skill_manage`, but did not finish content-level skill cleanup.

## Constraints
- Do not delete the skills framework or skill tools.
- Do not delete `AgentTool` / subagent orchestration.
- Do not delete Dreamer, todo, cron, scheduler, memory, soul/profile, player interaction primitives, MCP framework, media/voice/vision runtime tools.
- Delete or retire confirmed non-game content; do not leave it as a disabled/hidden alternate track.
- If removed content has UI exposure, the UI exposure must disappear too.
- Defer uncertain NPC-life-adjacent content rather than deleting it in this pass.

## Unknowns / Open Questions
- Whether the whole Skills navigation page should remain as a future NPC/game skill browser, or be removed until game-specific skills exist.
- Whether model/media-oriented skills such as Whisper, Stable Diffusion, CLIP, Segment Anything, and audio generation should remain as adjacent future game media capabilities or be pruned as ML workflow documentation.

## Likely Touchpoints
- `skills/**`
- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop/Views/SkillsPage.xaml`
- `Desktop/HermesDesktop/Views/SkillsPage.xaml.cs`
- `Desktop/HermesDesktop/Views/Panels/SkillsPanel.*`
- `Desktop/HermesDesktop/Views/ChatPage.xaml.cs`
- `src/skills/SkillManager.cs`
- `src/Tools/SkillManagementTools.cs`
- `src/Tools/SkillInvokeTool.cs`
