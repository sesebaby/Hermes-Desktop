# Session Storage + FTS5 Parity Evidence

## Reference
- Python session store: `external/hermes-agent-main/hermes_state.py`
- Python session search tool: `external/hermes-agent-main/tools/session_search_tool.py`

## Implemented Mapping
- C# `SessionSearchIndex` now owns Python-style `state.db` schema:
  - `schema_version` set to `9`
  - `sessions`
  - `messages`
  - `state_meta`
  - FTS5 `messages_fts`
  - source/parent/started/message indexes
- `TranscriptStore` is SQLite-first for session messages.
- Per-session transcript JSONL is not written or retained as runtime session storage.
- Desktop DI uses `$HERMES_HOME/hermes-cs/state.db`.
- FTS search supports:
  - default recallable roles: user/assistant
  - source include/exclude filters
  - hyphen/dot query sanitization
  - CJK LIKE fallback
- `DreamerService` now enumerates sessions through `TranscriptStore`, not transcript JSONL files.

## Verification
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter "TranscriptStoreTests"`
  - Result: 30 passed
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter "TranscriptStoreTests|MemoryParityTests|HermesChatServiceLogicTests"`
  - Result: 81 passed
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore`
  - Result: 596 passed
- `dotnet build HermesDesktop.sln --no-restore`
  - Result: succeeded, 0 warnings, 0 errors
- `dotnet build Desktop\\HermesDesktop\\HermesDesktop.csproj -c Debug -p:Platform=x64 --no-restore`
  - Result: succeeded, 0 warnings, 0 errors
- `git diff --check`
  - Result: exit 0; CRLF normalization warnings only

## Controlled Deviations
- Activity traces still use `.activity.jsonl`; they are not session message storage and are outside the Python session DB parity requirement.
- Some legacy planning documents still describe the earlier JSONL-authoritative plan; this evidence document supersedes that point for the current implementation.


