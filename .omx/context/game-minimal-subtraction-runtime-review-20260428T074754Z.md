# Context Snapshot: Game Minimal Subtraction Runtime Review

## Task Statement
Review `.omc/specs/深度访谈-游戏化最小减法运行时.md`, understand the user's need, and discuss whether the direction is correct, whether the content is accurate and complete, and what is missing.

## Desired Outcome
A focused discussion that validates or challenges the spec before any implementation or planning handoff.

## Stated Solution
The existing spec proposes turning Hermes Desktop into a game-oriented minimal subtraction runtime by removing or excluding clearly non-game capabilities while preserving core runtime density, MCP framework, skills framework, memory, soul, and future game bridge boundaries.

## Probable Intent Hypothesis
The user wants to avoid a wrong first implementation pass that either over-deletes useful Hermes capabilities or under-specifies the actual runtime boundary needed for game/mod integration.

## Known Facts / Evidence
- The spec file exists and is a prior deep-interview artifact with 8 rounds and final ambiguity 4.6%.
- The cited files exist: `src/Core/Agent.cs`, `src/Core/Models.cs`, `src/memory/MemoryManager.cs`, `src/soul/SoulService.cs`, `src/mcp/McpManager.cs`, `src/Tools/SkillInvokeTool.cs`, `src/skills/SkillManager.cs`, `src/gateway/*`, and `Desktop/HermesDesktop/App.xaml.cs`.
- `Desktop/HermesDesktop/App.xaml.cs` has a centralized `RegisterAllTools` method at lines 745-822, which supports a profile-based registration approach.
- CLI registration in `src/Program.cs` is already minimal: it registers `TerminalTool` as `ITool` around lines 94-110.
- The repo has many skill categories, including clearly non-game categories (`apple`, `claude-code`, `devops`, `email`, `github`, `productivity`, `software-development`, `smart-home`, `social-media`) and relevant/future-relevant categories (`gaming`, `media`, `creative`, `souls`).
- `ImageGenerationTool`, `TextToSpeechTool`, `TranscriptionTool`, and `VisionTool` exist in `src/Tools`, but current Desktop `RegisterAllTools` output did not show them registered.
- `BrowserTool` and `HomeAssistantTool` exist, but current Desktop `RegisterAllTools` output did not show them registered.
- No repo `mcp.json` was found by a simple file search, so MCP content pruning may currently be mostly a config/profile concern rather than repo-file deletion.

## Constraints
- Deep-interview mode is active; ask each interview round through `omx question`.
- Do not implement directly in deep-interview mode.
- Keep discussion focused on what to remove/exclude and why.
- Preserve MCP and skills frameworks.
- Preserve media/voice/vision capability for future game mod integration.
- Do not include Desktop UI host in the first deletion pass.

## Unknowns / Open Questions
- Should "retain media/voice/vision" mean retain source only, add them to game runtime, or merely reserve their future integration path?
- Should inactive-but-existing tools such as `BrowserTool` and `HomeAssistantTool` be treated as P1 deletion items, or just excluded from the game runtime manifest until they become active?
- Should the first deliverable be a conceptual deletion plan or a concrete `GameRuntimeProfile` / allowlist artifact?
- What is the intended boundary between "game runtime package exclusion" and physical source deletion?

## Decision-Boundary Unknowns
- Whether Codex may downgrade candidate physical deletions to "exclude from game runtime profile" without asking each time.
- Whether Codex may correct the existing spec artifact directly after discussion.
- Whether capability retention should be defined at source, registration, packaging, or runtime exposure level.

## Likely Codebase Touchpoints
- `Desktop/HermesDesktop/App.xaml.cs`
- `src/Program.cs`
- `src/Core/Agent.cs`
- `src/Tools/*.cs`
- `src/mcp/*.cs`
- `src/skills/*.cs`
- `skills/**`
- future game runtime profile / registration manifest location
