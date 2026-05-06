# C# Hermes Desktop Python Memory Runtime Parity

## Task Statement
Implement the identified gaps so C# Hermes Desktop aligns with `external/hermes-agent-main` for memory, session search, review nudges, runtime facts, terminal execution failure semantics, and security validation.

## Desired Outcome
The Desktop app should match the Python reference behavior closely enough that memory writes, session recall, background review, runtime tool use, and terminal failures behave predictably on Windows.

## Known Facts / Evidence
- Python reference uses curated `MEMORY.md` / `USER.md` for long-term memory and SQLite `state.db` with FTS5 for session search.
- Python reference injects memory guidance, session search guidance, and current-session context into the prompt.
- Python reference requires tool use for current time/date/timezone and system state.
- Python reference defaults `memory.nudge_interval` and `skills.creation_nudge_interval` to `10`.
- Current C# default thresholds were recently changed to `1`; user now wants code fallback default `5` and explicit local config `1`.
- Manual test showed "今天周几" led to Windows shell/tool failures and UI stuck thinking.
- Evidence indicates `Get-Date -Format dddd` was falsely blocked as disk formatting.

## Constraints
- Preserve user/unrelated changes, especially existing `.omx` and prior `App.xaml.cs` edits.
- Do not restore JSONL memory storage.
- Keep implementation grounded in current C# architecture, not a rewrite.
- Fix root causes, not a hardcoded answer for the weekday question.

## Unknowns / Open Questions
- Exact Desktop config file path for local explicit `nudge_interval: 1` must be confirmed from current code.
- Current terminal implementation and UI loop behavior must be inspected before patching.
- Existing tests may already cover part of memory/session parity; extend rather than duplicate.

## Likely Codebase Touchpoints
- `Desktop/HermesDesktop/App.xaml.cs`
- `src/Core/SystemPrompts.cs`
- `src/Core/MemoryReferenceText.cs`
- `src/Context/PromptBuilder.cs`
- `src/Tools/TerminalTool.cs`
- `src/Tools/BashTool.cs`
- `src/security/validators/Validators.cs`
- `src/memory/MemoryReviewService.cs`
- `src/search/SessionSearchIndex.cs`
- `Desktop/HermesDesktop.Tests`
