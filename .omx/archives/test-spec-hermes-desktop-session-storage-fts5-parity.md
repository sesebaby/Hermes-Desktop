# Test Spec: Session Storage + FTS5 Python Parity

## Unit/Integration Tests
- Creating a `TranscriptStore` with SQLite enabled creates Python-style schema and `schema_version`.
- Saving messages creates a session row and message rows in SQLite.
- Session counters increment: `message_count`; `tool_call_count` when assistant messages include tool calls.
- Saving session messages creates/updates SQLite `state.db` and does not create per-session JSONL transcript files.
- FTS search finds normal English content and returns session metadata fields.
- FTS search can filter by source and exclude hidden/delegated sources.
- FTS search handles hyphenated/dotted query terms without throwing.
- CJK query falls back to LIKE when FTS does not produce hits.
- Existing `TranscriptRecallService` and `SessionSearchTool` tests continue passing.

## Verification Commands
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter "TranscriptStoreTests|MemoryParityTests"`
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore`
- `dotnet build HermesDesktop.sln --no-restore`
- `git diff --check`
