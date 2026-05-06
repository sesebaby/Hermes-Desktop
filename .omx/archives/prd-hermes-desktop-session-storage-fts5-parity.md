# PRD: Hermes Desktop Session Storage + FTS5 Python Parity

## Goal
Make C# Hermes Desktop session storage and FTS5 behavior align with Python `external/hermes-agent-main/hermes_state.py` enough that session history is SQLite-backed, queryable, source-aware, parent-chain-aware, and durable across app restarts.

## Reference
- `external/hermes-agent-main/hermes_state.py`
- `external/hermes-agent-main/tools/session_search_tool.py`

## Acceptance Criteria
- SQLite database is the authoritative or primary-backed store for session metadata and messages, not only a sidecar search index.
- Schema includes Python-equivalent core fields: `sessions`, `messages`, `state_meta`, `schema_version`, FTS5 `messages_fts`, and indexes for source, parent, started time, and messages by session/timestamp.
- Writes create/update session rows and append message rows atomically enough for Desktop usage.
- Message writes maintain `message_count` and `tool_call_count` equivalent counters.
- FTS search supports source include/exclude filters and excludes non-recallable tool messages at recall layer.
- FTS query sanitization handles punctuation/hyphen/dot terms and has a CJK LIKE fallback like Python.
- Session messages are stored in SQLite `state.db`; per-session JSONL transcript files are not retained as a runtime storage path.
- Desktop uses Python-style `state.db` path under `$HERMES_HOME/hermes-cs/state.db` or documented equivalent.
- Tests cover schema, writes, no session JSONL output, search filters, CJK fallback, and existing recall behavior.
- Build and targeted/full tests pass before commit.

## Non-Goals
- Port every Python billing/cost field consumer if no C# caller exists.
- Replace curated memory `MEMORY.md` / `USER.md`; that is intentionally file-backed.
- Add external vector stores or new packages.
