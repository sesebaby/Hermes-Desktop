# Python Skill Self-Evolution Reference Mapping

## Scope
Reference-first mapping for Python `external/hermes-agent-main` skill/procedural-memory self-evolution into C# Hermes Desktop.

## Reference Chain
1. Trigger: Python tracks `_iters_since_skill` and queues review when `skills.creation_nudge_interval` is reached; `skill_manage` resets the counter.
2. Snapshot: Python forks a background review agent with the prior conversation snapshot after user-visible response delivery.
3. Prompt / Summary Assembly: Python selects memory-only, skill-only, or combined review prompts and appends tool-aware `SKILLS_GUIDANCE` only when `skill_manage` exists. It also injects a mandatory skills index that instructs the model to scan and load relevant skills with `skill_view`.
4. Parse / Repair / Normalize: Python exposes schemas for `skills_list`, `skill_view`, and `skill_manage` with snake_case parameters and action enum `create|patch|edit|delete|write_file|remove_file`; `patch` uses fuzzy matching rather than exact replacement.
5. Projector / Executor: Python executes review tool calls in a forked agent; skills are procedural memory and prefer patching existing skills after surveying.
6. Authoritative Writeback: Python writes local directory-based skills as `SKILL.md` plus supporting files under `references/`, `templates/`, `scripts/`, or `assets/`; path validation resolves links and rejects escapes.
7. Player-visible Surface: Python does not mutate the foreground conversation; review is post-response and best-effort, with optional compact action summaries.

## Current C# Mapping
| Reference layer | C# implementation | Status |
| --- | --- | --- |
| Trigger | `Agent` tracks tool-loop iterations and `skill_manage` usage, passes both to `MemoryReviewService.QueueAfterTurn`; service applies skill nudge counter. | Equivalent mapping |
| Snapshot | `QueueAfterTurn` clones message snapshot and runs detached `Task.Run`. | Equivalent mapping |
| Prompt assembly | `MemoryReferenceText.SkillsGuidance`, dynamic `PromptBuilder`, and `SkillManager.BuildSkillsMandatoryPrompt()` provide Python-style guidance plus `## Skills (mandatory)` index. | Equivalent mapping |
| Tool schemas | `SkillsListTool`, `SkillViewTool`, `SkillManageTool` with Python action enum and snake_case fields. | Equivalent mapping |
| Patch normalization | `FuzzyTextReplacer` implements the Python fuzzy patch strategy chain and returns previews/hints on misses. | Equivalent mapping |
| Path hardening | `SkillManager` resolves existing links via `FileSystemInfo.ResolveLinkTarget(true)` and validates with `Path.GetRelativePath`, matching Python `resolve()+relative_to()` intent. | Equivalent mapping |
| Executor loop | `MemoryReviewService` executes review tool calls for up to 8 iterations and appends assistant/tool messages only to the review copy. | Equivalent mapping |
| Writeback | `SkillManager.CreateSkillFromContentAsync`, patch/edit/delete, supporting-file write/remove under allowed subdirs. | Equivalent mapping |
| Visible surface | Foreground `Session.Messages` receives no review messages; optional `BackgroundReviewSummaryCallback` can surface compact review actions. | Equivalent mapping |

## Accepted Adaptations
- C# keeps existing `SkillManager` as the authority instead of Python module globals.
- Existing legacy flat `.md` skills remain viewable/invokable; mutation/supporting-file writes target directory-based `SKILL.md` skills, matching Python writeback semantics.
- Legacy `ReviewConversationAsync(messages, ct)` remains single-pass for existing tests; the new explicit memory/skill review overload uses the Python-style multi-step loop.
- Symlink escape regression is implemented in code and covered by a test that is skipped only when the OS/test environment cannot create symlinks.
