# Dream Reference Alignment Context

## Task Statement

Plan how to make the current Hermes-Desktop dream / long-term memory capability align more closely with the reference project at `external/hermes-agent-main`.

## Desired Outcome

Produce a consensus implementation plan, not source changes, for improving current dream completeness. The likely target is to preserve the current `src/dreamer` local free-association worker while closing gaps against the reference project's closed learning loop: persistent memory, session search recall, context-compression handoff, and memory provider lifecycle.

## Known Facts / Evidence

- Reference project does not expose a standalone `dreamer` module. Its closest equivalent is a learning and recall loop documented as "Agent-curated memory with periodic nudges" plus "FTS5 session search with LLM summarization" in `external/hermes-agent-main/README.md:21`.
- Reference persistent memory stores `MEMORY.md` and `USER.md` under `~/.hermes/memories/`, injects a frozen snapshot at session start, and uses a bounded `memory` tool. Evidence: `external/hermes-agent-main/website/docs/user-guide/features/memory.md:13`, `external/hermes-agent-main/tools/memory_tool.py:1`.
- Reference session search is separate from curated memory and searches all stored sessions via FTS5 with LLM summarization. Evidence: `external/hermes-agent-main/website/docs/user-guide/features/memory.md:175`, `external/hermes-agent-main/tools/session_search_tool.py:326`.
- Reference context compression calls memory provider pre-compression hooks and commits memory before session id rotation. Evidence: `external/hermes-agent-main/run_agent.py:8102`, `external/hermes-agent-main/run_agent.py:8137`.
- Current project has a `src/dreamer` feature set: `DreamerService`, `DreamWalk`, `SignalScorer`, `EchoDetector`, `RssFetcher`, `BuildSprint`, `DreamerRoom`, `DreamerConfig`, and `DreamerStatus`.
- Current desktop app starts `DreamerService` from `Desktop/HermesDesktop/App.xaml.cs:633` and constructs it at `Desktop/HermesDesktop/App.xaml.cs:662`.
- Current `DreamerService` performs RSS refresh, research context build, walk generation, echo scoring, signal scoring, optional build sprint, local digest, insights save, and dashboard status updates. Evidence: `src/dreamer/DreamerService.cs:120`.
- Current `DreamWalk` reads Dreamer soul/fascinations/research/prior walk, calls the configured chat client, and writes a markdown walk. Evidence: `src/dreamer/DreamWalk.cs:33`.
- Current `BuildSprint` only scaffolds sandbox docs and explicitly defers full agent tool execution. Evidence: `src/dreamer/BuildSprint.cs:22`.
- Current `AutoDreamService` exists and can scan transcripts, call `ConsolidationAgent`, write memory files, and run soul extraction. Evidence: `src/dream/AutoDreamService.cs:47`.
- Current `AutoDreamService` appears unregistered in runtime startup. Targeted search for `AutoDreamService`, `AddHostedService`, and `ConsolidationAgent` in startup/project files found only `DreamerService` startup in `Desktop/HermesDesktop/App.xaml.cs`.
- Current settings UI exposes some Dreamer values but preserves many hidden config values instead of offering controls for them. Evidence: `Desktop/HermesDesktop/Views/SettingsPage.xaml.cs:641`, `Desktop/HermesDesktop/Views/SettingsPage.xaml:412`.
- Current Dreamer component tests pass: `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --filter "FullyQualifiedName~Dreamer|FullyQualifiedName~RssFetcher|FullyQualifiedName~InsightsDreamer"` returned 160 passed, 0 failed.

## Constraints

- Planning only under `$ralplan`; no source implementation in this phase.
- Preserve existing behavior unless explicitly scoped otherwise.
- No new dependencies without explicit request.
- Current workspace has unrelated dirty changes in Stardew bridge files; planning artifacts should avoid touching them.
- The reference project is not a direct module parity target by name; compare by capability and authority flow.

## Unknowns / Open Questions

- Whether the user wants exact parity with reference memory behavior or a title-local adaptation that keeps the separate Dreamer identity.
- Whether AutoDream should be revived as the authoritative memory consolidator, merged into DreamerService, or retired in favor of the existing TurnMemoryCoordinator/TranscriptMemoryProvider path.
- Whether Dreamer build sprints should remain notes-only or be allowed to run an agent inside the sandbox.
- Whether local-only Ollama remains a hard requirement for Dreamer walks and echo scoring.

## Likely Codebase Touchpoints

- `Desktop/HermesDesktop/App.xaml.cs`
- `Desktop/HermesDesktop/Views/SettingsPage.xaml`
- `Desktop/HermesDesktop/Views/SettingsPage.xaml.cs`
- `Desktop/HermesDesktop/Views/DashboardPage.xaml.cs`
- `src/dreamer/DreamerService.cs`
- `src/dreamer/DreamerConfig.cs`
- `src/dreamer/DreamWalk.cs`
- `src/dreamer/SignalScorer.cs`
- `src/dreamer/BuildSprint.cs`
- `src/dream/AutoDreamService.cs`
- `src/memory/*`
- `src/search/*`
- `src/soul/*`
- `Desktop/HermesDesktop.Tests/Dreamer/*`
- `Desktop/HermesDesktop.Tests/Services/*`
