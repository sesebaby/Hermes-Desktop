---
title: Skill System
type: system
tags: [skills, tools]
created: 2026-04-09
updated: 2026-04-09
sources: [src/skills/skillmanager.cs, src/skills/SkillsHub.cs]
---

# Skill System

Markdown-based custom agent capabilities with YAML frontmatter.

## Skill Format

```markdown
---
name: skill-name
description: One-line description
tools: read_file, write_file, bash
model: optional-model-override
---

System prompt instructions for the skill...
```

## SkillManager

Constructor: `SkillManager(string skillsDir, ILogger<SkillManager> logger)`

On init, recursively loads all `*.md` files from skillsDir with `SearchOption.AllDirectories`.

Key methods:
- `GetSkill(name)` -- lookup by name from ConcurrentDictionary
- `ListSkills()` -- all loaded skills
- `InvokeSkillAsync(name, query, ct)` -- builds context string with skill instructions + user query
- `CreateSkillAsync(name, description, systemPrompt, tools, model?, category?, ct)` -- validated creation
- `EditSkillAsync(name, newContent, ct)` -- full rewrite with rollback
- `PatchSkillAsync(name, oldText, newText, replaceAll, ct)` -- targeted find-and-replace
- `DeleteSkillAsync(name, ct)` -- removes file and cache entry

## Validation (upstream patterns from skill_manager_tool.py)

- Name: `^[a-z0-9][a-z0-9._-]*$`, max 64 chars
- Description: max 1024 chars
- Content: max 100,000 chars
- Duplicate detection: rejects if name already exists

## Atomic Write Pattern

All mutations use temp-file + rename:
```csharp
File.WriteAllTextAsync(tempPath, content, ct);
File.Move(tempPath, path, overwrite: true);
```
On failure, temp file is cleaned up. After write, SecretScanner checks content and rolls back if secrets detected.

## SkillInvoker

Wraps SkillManager + IChatClient for one-shot invocations:
```csharp
var skillContext = await _skillManager.InvokeSkillAsync(skillName, userQuery, ct);
var response = await _chatClient.CompleteAsync(new[] { systemMessage }, ct);
```

## Built-in Skills

There is no separate hardcoded built-in skill catalog in the current repo snapshot. Runtime-visible bundled skills are loaded from disk by `SkillManager`, and the shipping set is constrained by `.bundled-skills-manifest.json`.

## Disk Layout

Skills directory at repo root: `skills/` with 10 active category subdirectories containing 21 repo skill definitions. The checked-in manifest currently ships 13 retained skills and keeps 8 more as deferred, non-shipping sources.

## Key Files
- `src/skills/skillmanager.cs` -- SkillManager, Skill, SkillInvoker, BuiltInSkills (~535 lines)
- `src/skills/SkillsHub.cs` -- additional skill management
- `skills/` -- 21 repo skill files across 10 active categories, with shipping controlled by `.bundled-skills-manifest.json`

## See Also
- [[tool-system]]
- [[soul-system]]
