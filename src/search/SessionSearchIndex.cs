namespace Hermes.Agent.Search;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hermes.Agent.Core;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

/// <summary>
/// SQLite-backed session state store with FTS5 search.
/// Mirrors the core shape of Python hermes_state.py: sessions, messages,
/// state_meta, schema_version, and messages_fts in one state.db-style file.
/// </summary>
public sealed class SessionSearchIndex : IDisposable
{
    public const int SchemaVersion = 10;

    private readonly string _connectionString;
    private readonly ILogger<SessionSearchIndex> _logger;
    private readonly object _gate = new();

    public SessionSearchIndex(string dbPath, ILogger<SessionSearchIndex> logger)
    {
        _logger = logger;
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? ".");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Pooling = false
        }.ToString();
        InitializeSchema();
    }

    private SqliteConnection OpenConnection()
    {
        var db = new SqliteConnection(_connectionString);
        db.Open();
        using var pragma = db.CreateCommand();
        pragma.CommandText = @"
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();
        return db;
    }

    private void InitializeSchema()
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS schema_version (
                    version INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS sessions (
                    id TEXT PRIMARY KEY,
                    source TEXT NOT NULL,
                    user_id TEXT,
                    model TEXT,
                    model_config TEXT,
                    system_prompt TEXT,
                    parent_session_id TEXT,
                    started_at REAL NOT NULL,
                    ended_at REAL,
                    end_reason TEXT,
                    message_count INTEGER DEFAULT 0,
                    tool_call_count INTEGER DEFAULT 0,
                    input_tokens INTEGER DEFAULT 0,
                    output_tokens INTEGER DEFAULT 0,
                    cache_read_tokens INTEGER DEFAULT 0,
                    cache_write_tokens INTEGER DEFAULT 0,
                    reasoning_tokens INTEGER DEFAULT 0,
                    billing_provider TEXT,
                    billing_base_url TEXT,
                    billing_mode TEXT,
                    estimated_cost_usd REAL,
                    actual_cost_usd REAL,
                    cost_status TEXT,
                    cost_source TEXT,
                    pricing_version TEXT,
                    title TEXT,
                    api_call_count INTEGER DEFAULT 0,
                    FOREIGN KEY (parent_session_id) REFERENCES sessions(id)
                );

                CREATE TABLE IF NOT EXISTS messages (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    session_id TEXT NOT NULL REFERENCES sessions(id),
                    role TEXT NOT NULL,
                    content TEXT,
                    tool_call_id TEXT,
                    tool_calls TEXT,
                    tool_name TEXT,
                    timestamp REAL NOT NULL,
                    token_count INTEGER,
                    finish_reason TEXT,
                    task_session_id TEXT,
                    reasoning TEXT,
                    reasoning_content TEXT,
                    reasoning_details TEXT,
                    codex_reasoning_items TEXT,
                    codex_message_items TEXT
                );

                CREATE TABLE IF NOT EXISTS state_meta (
                    key TEXT PRIMARY KEY,
                    value TEXT
                );

                CREATE TABLE IF NOT EXISTS activities (
                    session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
                    id TEXT NOT NULL,
                    timestamp REAL NOT NULL,
                    sequence INTEGER NOT NULL,
                    duration_ms INTEGER DEFAULT 0,
                    tool_name TEXT NOT NULL,
                    tool_call_id TEXT,
                    input_summary TEXT,
                    output_summary TEXT,
                    status TEXT NOT NULL,
                    diff_preview TEXT,
                    screenshot_path TEXT,
                    PRIMARY KEY (session_id, id)
                );

                CREATE INDEX IF NOT EXISTS idx_sessions_source ON sessions(source);
                CREATE INDEX IF NOT EXISTS idx_sessions_parent ON sessions(parent_session_id);
                CREATE INDEX IF NOT EXISTS idx_sessions_started ON sessions(started_at DESC);
                CREATE INDEX IF NOT EXISTS idx_messages_session ON messages(session_id, timestamp);
                CREATE INDEX IF NOT EXISTS idx_activities_session ON activities(session_id, timestamp);

                CREATE VIRTUAL TABLE IF NOT EXISTS messages_fts USING fts5(
                    content,
                    content='messages',
                    content_rowid='id',
                    tokenize='porter unicode61'
                );

                CREATE TRIGGER IF NOT EXISTS messages_fts_insert AFTER INSERT ON messages BEGIN
                    INSERT INTO messages_fts(rowid, content) VALUES (new.id, new.content);
                END;
                CREATE TRIGGER IF NOT EXISTS messages_fts_delete AFTER DELETE ON messages BEGIN
                    INSERT INTO messages_fts(messages_fts, rowid, content) VALUES('delete', old.id, old.content);
                END;
                CREATE TRIGGER IF NOT EXISTS messages_fts_update AFTER UPDATE ON messages BEGIN
                    INSERT INTO messages_fts(messages_fts, rowid, content) VALUES('delete', old.id, old.content);
                    INSERT INTO messages_fts(rowid, content) VALUES (new.id, new.content);
                END;";
            cmd.ExecuteNonQuery();

            EnsureMessagesColumns(db);
            EnsureMessagesIndexes(db);

            using var version = db.CreateCommand();
            version.CommandText = @"
                INSERT INTO schema_version (version)
                SELECT $version
                WHERE NOT EXISTS (SELECT 1 FROM schema_version);
                UPDATE schema_version SET version = $version;";
            version.Parameters.AddWithValue("$version", SchemaVersion);
            version.ExecuteNonQuery();
        }
    }

    private static void EnsureMessagesColumns(SqliteConnection db)
    {
        var existing = ReadColumnNames(db, "messages");
        AddColumnIfMissing(db, existing, "task_session_id", "TEXT");
        AddColumnIfMissing(db, existing, "reasoning", "TEXT");
        AddColumnIfMissing(db, existing, "reasoning_content", "TEXT");
        AddColumnIfMissing(db, existing, "reasoning_details", "TEXT");
        AddColumnIfMissing(db, existing, "codex_reasoning_items", "TEXT");
        AddColumnIfMissing(db, existing, "codex_message_items", "TEXT");
    }

    private static void EnsureMessagesIndexes(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = @"
            CREATE INDEX IF NOT EXISTS idx_messages_task_session_tool
            ON messages(task_session_id, role, tool_name, timestamp, id);";
        cmd.ExecuteNonQuery();
    }

    private static HashSet<string> ReadColumnNames(SqliteConnection db, string tableName)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = cmd.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
            columns.Add(reader.GetString(1));
        return columns;
    }

    private static void AddColumnIfMissing(SqliteConnection db, HashSet<string> existing, string name, string type)
    {
        if (existing.Contains(name))
            return;

        using var cmd = db.CreateCommand();
        cmd.CommandText = $"ALTER TABLE messages ADD COLUMN {name} {type}";
        cmd.ExecuteNonQuery();
        existing.Add(name);
    }

    /// <summary>Compatibility API for older callers; writes into the full SQLite state store.</summary>
    public void IndexMessage(string sessionId, string role, string content, DateTime timestamp)
        => SaveMessage(sessionId, new Message { Role = role, Content = content, Timestamp = timestamp });

    public long SaveMessage(
        string sessionId,
        Message message,
        string source = "desktop",
        string? model = null,
        string? userId = null,
        string? parentSessionId = null,
        string? systemPrompt = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session id cannot be empty.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(message.Role))
            throw new ArgumentException("Message role cannot be empty.", nameof(message));

        lock (_gate)
        {
            using var db = OpenConnection();
            using var tx = db.BeginTransaction();
            try
            {
                EnsureSession(db, sessionId, source, model, userId, parentSessionId, systemPrompt, message.Timestamp);
                var id = InsertMessage(db, sessionId, message);
                tx.Commit();
                return id;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    public void ReplaceSessionMessages(string sessionId, IReadOnlyList<Message> messages, string source = "desktop")
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var tx = db.BeginTransaction();
            try
            {
                DeleteSession(db, sessionId);
                foreach (var message in messages)
                {
                    EnsureSession(db, sessionId, source, model: null, userId: null, parentSessionId: null, systemPrompt: null, message.Timestamp);
                    InsertMessage(db, sessionId, message);
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    public List<Message> LoadMessages(string sessionId)
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"
                SELECT role, content, tool_call_id, tool_calls, tool_name, timestamp, task_session_id,
                       reasoning, reasoning_content, reasoning_details, codex_reasoning_items
                FROM messages
                WHERE session_id = $session_id
                ORDER BY id";
            cmd.Parameters.AddWithValue("$session_id", sessionId);
            using var reader = cmd.ExecuteReader();
            var messages = new List<Message>();
            while (reader.Read())
                messages.Add(ReadMessage(reader));

            return messages;
        }
    }

    public IReadOnlyList<Message> LoadTodoToolResultsByTaskSessionId(
        string taskSessionId,
        string legacyPrivateChatPrefix,
        bool includeLegacyFallback)
    {
        if (string.IsNullOrWhiteSpace(taskSessionId))
            throw new ArgumentException("Task session id cannot be empty.", nameof(taskSessionId));
        if (includeLegacyFallback && string.IsNullOrWhiteSpace(legacyPrivateChatPrefix))
            throw new ArgumentException("Legacy private chat prefix cannot be empty when fallback is enabled.", nameof(legacyPrivateChatPrefix));
        if (includeLegacyFallback && !legacyPrivateChatPrefix.EndsWith(":private_chat:", StringComparison.Ordinal))
            throw new ArgumentException("Legacy private chat prefix must end with ':private_chat:'.", nameof(legacyPrivateChatPrefix));

        lock (_gate)
        {
            using var db = OpenConnection();
            var explicitResults = LoadTodoToolResults(
                db,
                @"
                task_session_id = $task_session_id",
                command =>
                {
                    command.Parameters.AddWithValue("$task_session_id", taskSessionId);
                });
            if (explicitResults.Count > 0 || !includeLegacyFallback)
                return explicitResults;

            return LoadTodoToolResults(
                db,
                @"
                (session_id = $task_session_id OR substr(session_id, 1, length($legacy_private_chat_prefix)) = $legacy_private_chat_prefix)",
                command =>
                {
                    command.Parameters.AddWithValue("$task_session_id", taskSessionId);
                    command.Parameters.AddWithValue("$legacy_private_chat_prefix", legacyPrivateChatPrefix);
                });
        }
    }

    public void SaveActivity(string sessionId, ActivityEntry entry, string source = "desktop")
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session id cannot be empty.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(entry.ToolName))
            throw new ArgumentException("Activity tool name cannot be empty.", nameof(entry));

        lock (_gate)
        {
            using var db = OpenConnection();
            using var tx = db.BeginTransaction();
            try
            {
                EnsureSession(db, sessionId, source, model: null, userId: null, parentSessionId: null, systemPrompt: null, entry.Timestamp);
                using var cmd = db.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO activities (
                        session_id, id, timestamp, sequence, duration_ms, tool_name,
                        tool_call_id, input_summary, output_summary, status,
                        diff_preview, screenshot_path
                    )
                    VALUES (
                        $session_id, $id, $timestamp, $sequence, $duration_ms, $tool_name,
                        $tool_call_id, $input_summary, $output_summary, $status,
                        $diff_preview, $screenshot_path
                    )
                    ON CONFLICT(session_id, id) DO UPDATE SET
                        timestamp = excluded.timestamp,
                        sequence = excluded.sequence,
                        duration_ms = excluded.duration_ms,
                        tool_name = excluded.tool_name,
                        tool_call_id = excluded.tool_call_id,
                        input_summary = excluded.input_summary,
                        output_summary = excluded.output_summary,
                        status = excluded.status,
                        diff_preview = excluded.diff_preview,
                        screenshot_path = excluded.screenshot_path";
                cmd.Parameters.AddWithValue("$session_id", sessionId);
                cmd.Parameters.AddWithValue("$id", entry.Id);
                cmd.Parameters.AddWithValue("$timestamp", ToUnixSeconds(entry.Timestamp));
                cmd.Parameters.AddWithValue("$sequence", entry.Sequence);
                cmd.Parameters.AddWithValue("$duration_ms", entry.DurationMs);
                cmd.Parameters.AddWithValue("$tool_name", entry.ToolName);
                cmd.Parameters.AddWithValue("$tool_call_id", (object?)entry.ToolCallId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$input_summary", entry.InputSummary);
                cmd.Parameters.AddWithValue("$output_summary", entry.OutputSummary);
                cmd.Parameters.AddWithValue("$status", entry.Status.ToString());
                cmd.Parameters.AddWithValue("$diff_preview", (object?)entry.DiffPreview ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$screenshot_path", (object?)entry.ScreenshotPath ?? DBNull.Value);
                cmd.ExecuteNonQuery();
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    public List<ActivityEntry> LoadActivities(string sessionId)
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"
                SELECT id, timestamp, sequence, duration_ms, tool_name, tool_call_id,
                       input_summary, output_summary, status, diff_preview, screenshot_path
                FROM activities
                WHERE session_id = $session_id
                ORDER BY timestamp, sequence, id";
            cmd.Parameters.AddWithValue("$session_id", sessionId);
            using var reader = cmd.ExecuteReader();
            var entries = new List<ActivityEntry>();
            while (reader.Read())
            {
                entries.Add(new ActivityEntry
                {
                    Id = reader.GetString(0),
                    Timestamp = FromUnixSeconds(reader.GetDouble(1)),
                    Sequence = reader.GetInt64(2),
                    DurationMs = reader.GetInt64(3),
                    ToolName = reader.GetString(4),
                    ToolCallId = reader.IsDBNull(5) ? null : reader.GetString(5),
                    InputSummary = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    OutputSummary = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    Status = ParseActivityStatus(reader.IsDBNull(8) ? null : reader.GetString(8)),
                    DiffPreview = reader.IsDBNull(9) ? null : reader.GetString(9),
                    ScreenshotPath = reader.IsDBNull(10) ? null : reader.GetString(10)
                });
            }

            return entries;
        }
    }

    public bool SessionExists(string sessionId)
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sessions WHERE id = $session_id";
            cmd.Parameters.AddWithValue("$session_id", sessionId);
            return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
        }
    }

    public SessionMetadata? GetSessionMetadata(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"
                SELECT id, source, parent_session_id, model
                FROM sessions
                WHERE id = $session_id";
            cmd.Parameters.AddWithValue("$session_id", sessionId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            return new SessionMetadata(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3));
        }
    }

    public bool IsChildSession(string sessionId)
        => !string.IsNullOrWhiteSpace(GetSessionMetadata(sessionId)?.ParentSessionId);

    public string ResolveRootSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return sessionId;

        lock (_gate)
        {
            using var db = OpenConnection();
            var current = sessionId;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT parent_session_id FROM sessions WHERE id = $session_id";
                cmd.Parameters.AddWithValue("$session_id", current);
                var parent = cmd.ExecuteScalar();
                if (parent is null or DBNull)
                    break;

                var parentId = parent.ToString();
                if (string.IsNullOrWhiteSpace(parentId))
                    break;

                current = parentId;
            }

            return current;
        }
    }

    public List<string> ListSessionIds()
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT id FROM sessions ORDER BY started_at DESC";
            using var reader = cmd.ExecuteReader();
            var ids = new List<string>();
            while (reader.Read())
                ids.Add(reader.GetString(0));
            return ids;
        }
    }

    public List<string> ListSessionIdsByRecentActivity(int maxResults)
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"
                SELECT s.id
                FROM sessions s
                LEFT JOIN messages m ON m.session_id = s.id
                GROUP BY s.id, s.started_at
                ORDER BY COALESCE(MAX(m.timestamp), s.started_at) DESC
                LIMIT $limit";
            cmd.Parameters.AddWithValue("$limit", Math.Clamp(maxResults, 1, 1000));
            using var reader = cmd.ExecuteReader();
            var ids = new List<string>();
            while (reader.Read())
                ids.Add(reader.GetString(0));
            return ids;
        }
    }

    public long GetSessionMessageCount(string sessionId)
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM messages WHERE session_id = $session_id";
            cmd.Parameters.AddWithValue("$session_id", sessionId);
            return (long)(cmd.ExecuteScalar() ?? 0L);
        }
    }

    public void SetStateMeta(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("State metadata key cannot be empty.", nameof(key));

        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO state_meta (key, value)
                VALUES ($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public List<SearchResult> Search(
        string query,
        int maxResults = 10,
        IReadOnlyCollection<string>? sourceFilter = null,
        IReadOnlyCollection<string>? excludeSources = null,
        IReadOnlyCollection<string>? roleFilter = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SearchResult>();

        var sanitized = SanitizeQuery(query);
        if (string.IsNullOrWhiteSpace(sanitized))
            return new List<SearchResult>();

        try
        {
            var results = SearchFts(sanitized, maxResults, sourceFilter, excludeSources, roleFilter);
            if (results.Count > 0 || !ContainsCjk(sanitized))
                return results;

            return SearchLikeForCjk(sanitized.Trim('"'), maxResults, sourceFilter, excludeSources, roleFilter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FTS5 search failed for query: {Query}", query);
            if (ContainsCjk(sanitized))
                return SearchLikeForCjk(sanitized.Trim('"'), maxResults, sourceFilter, excludeSources, roleFilter);
            return new List<SearchResult>();
        }
    }

    public void DeleteSession(string sessionId)
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var tx = db.BeginTransaction();
            DeleteSession(db, sessionId);
            tx.Commit();
        }
    }

    /// <summary>Delete all session state.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var tx = db.BeginTransaction();
            using var deleteMessages = db.CreateCommand();
            deleteMessages.CommandText = "DELETE FROM messages";
            deleteMessages.ExecuteNonQuery();
            using var deleteActivities = db.CreateCommand();
            deleteActivities.CommandText = "DELETE FROM activities";
            deleteActivities.ExecuteNonQuery();
            using var deleteSessions = db.CreateCommand();
            deleteSessions.CommandText = "DELETE FROM sessions";
            deleteSessions.ExecuteNonQuery();
            tx.Commit();
        }
    }

    public long MessageCount()
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM messages";
            return (long)(cmd.ExecuteScalar() ?? 0L);
        }
    }

    public bool ContainsNonRecallableRoles()
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM messages
                WHERE LOWER(role) NOT IN ('user', 'assistant')";
            return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
        }
    }

    private static long InsertMessage(SqliteConnection db, string sessionId, Message message)
    {
        var toolCallsJson = message.ToolCalls is { Count: > 0 }
            ? JsonSerializer.Serialize(message.ToolCalls, JsonOptions)
            : null;
        using var cmd = db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO messages (
                session_id, role, content, tool_call_id, tool_calls, tool_name, timestamp, task_session_id,
                reasoning, reasoning_content, reasoning_details, codex_reasoning_items
            )
            VALUES (
                $session_id, $role, $content, $tool_call_id, $tool_calls, $tool_name, $timestamp, $task_session_id,
                $reasoning, $reasoning_content, $reasoning_details, $codex_reasoning_items
            )";
        cmd.Parameters.AddWithValue("$session_id", sessionId);
        cmd.Parameters.AddWithValue("$role", message.Role);
        cmd.Parameters.AddWithValue("$content", (object?)message.Content ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tool_call_id", (object?)message.ToolCallId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tool_calls", (object?)toolCallsJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tool_name", (object?)message.ToolName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$timestamp", ToUnixSeconds(message.Timestamp));
        cmd.Parameters.AddWithValue("$task_session_id", (object?)message.TaskSessionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$reasoning", (object?)message.Reasoning ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$reasoning_content", (object?)message.ReasoningContent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$reasoning_details", (object?)message.ReasoningDetails ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$codex_reasoning_items", (object?)message.CodexReasoningItems ?? DBNull.Value);
        cmd.ExecuteNonQuery();

        using var rowId = db.CreateCommand();
        rowId.CommandText = "SELECT last_insert_rowid()";
        var id = (long)(rowId.ExecuteScalar() ?? 0L);

        using var update = db.CreateCommand();
        update.CommandText = @"
            UPDATE sessions
            SET message_count = message_count + 1,
                tool_call_count = tool_call_count + $tool_count
            WHERE id = $session_id";
        update.Parameters.AddWithValue("$tool_count", message.ToolCalls?.Count ?? 0);
        update.Parameters.AddWithValue("$session_id", sessionId);
        update.ExecuteNonQuery();
        return id;
    }

    private static List<Message> LoadTodoToolResults(
        SqliteConnection db,
        string sessionPredicate,
        Action<SqliteCommand> bindSessionParameters)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = $@"
            SELECT role, content, tool_call_id, tool_calls, tool_name, timestamp, task_session_id,
                   reasoning, reasoning_content, reasoning_details, codex_reasoning_items
            FROM messages
            WHERE {sessionPredicate}
              AND LOWER(role) = 'tool'
              AND LOWER(tool_name) IN ('todo', 'todo_write')
            ORDER BY timestamp, id";
        bindSessionParameters(cmd);
        using var reader = cmd.ExecuteReader();
        var messages = new List<Message>();
        while (reader.Read())
            messages.Add(ReadMessage(reader));

        return messages;
    }

    private static Message ReadMessage(SqliteDataReader reader)
    {
        var toolCallsJson = reader.IsDBNull(3) ? null : reader.GetString(3);
        return new Message
        {
            Role = reader.GetString(0),
            Content = reader.IsDBNull(1) ? "" : reader.GetString(1),
            ToolCallId = reader.IsDBNull(2) ? null : reader.GetString(2),
            ToolCalls = DeserializeToolCalls(toolCallsJson),
            ToolName = reader.IsDBNull(4) ? null : reader.GetString(4),
            Timestamp = FromUnixSeconds(reader.GetDouble(5)),
            TaskSessionId = reader.IsDBNull(6) ? null : reader.GetString(6),
            Reasoning = reader.IsDBNull(7) ? null : reader.GetString(7),
            ReasoningContent = reader.IsDBNull(8) ? null : reader.GetString(8),
            ReasoningDetails = reader.IsDBNull(9) ? null : reader.GetString(9),
            CodexReasoningItems = reader.IsDBNull(10) ? null : reader.GetString(10)
        };
    }

    private static void EnsureSession(
        SqliteConnection db,
        string sessionId,
        string source,
        string? model,
        string? userId,
        string? parentSessionId,
        string? systemPrompt,
        DateTime timestamp)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO sessions (id, source, user_id, model, system_prompt, parent_session_id, started_at)
            VALUES ($id, $source, $user_id, $model, $system_prompt, $parent_session_id, $started_at)";
        cmd.Parameters.AddWithValue("$id", sessionId);
        cmd.Parameters.AddWithValue("$source", string.IsNullOrWhiteSpace(source) ? "desktop" : source);
        cmd.Parameters.AddWithValue("$user_id", (object?)userId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$model", (object?)model ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$system_prompt", (object?)systemPrompt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$parent_session_id", (object?)parentSessionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$started_at", ToUnixSeconds(timestamp));
        cmd.ExecuteNonQuery();
    }

    private static void DeleteSession(SqliteConnection db, string sessionId)
    {
        using var deleteActivities = db.CreateCommand();
        deleteActivities.CommandText = "DELETE FROM activities WHERE session_id = $session_id";
        deleteActivities.Parameters.AddWithValue("$session_id", sessionId);
        deleteActivities.ExecuteNonQuery();

        using var deleteMessages = db.CreateCommand();
        deleteMessages.CommandText = "DELETE FROM messages WHERE session_id = $session_id";
        deleteMessages.Parameters.AddWithValue("$session_id", sessionId);
        deleteMessages.ExecuteNonQuery();

        using var deleteSession = db.CreateCommand();
        deleteSession.CommandText = "DELETE FROM sessions WHERE id = $session_id";
        deleteSession.Parameters.AddWithValue("$session_id", sessionId);
        deleteSession.ExecuteNonQuery();
    }

    private List<SearchResult> SearchFts(
        string query,
        int maxResults,
        IReadOnlyCollection<string>? sourceFilter,
        IReadOnlyCollection<string>? excludeSources,
        IReadOnlyCollection<string>? roleFilter)
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            var where = new List<string> { "messages_fts MATCH $query" };
            cmd.Parameters.AddWithValue("$query", query);
            AppendFilters(cmd, where, sourceFilter, excludeSources, roleFilter);
            cmd.Parameters.AddWithValue("$limit", Math.Clamp(maxResults, 1, 100));

            cmd.CommandText = $@"
                SELECT m.session_id, m.role, m.content,
                       snippet(messages_fts, 0, '»', '«', '...', 40) AS snippet,
                       rank, m.timestamp, m.tool_name, s.source, s.model, s.started_at
                FROM messages_fts
                JOIN messages m ON messages_fts.rowid = m.id
                JOIN sessions s ON s.id = m.session_id
                WHERE {string.Join(" AND ", where)}
                ORDER BY rank
                LIMIT $limit";
            using var reader = cmd.ExecuteReader();
            return ReadSearchResults(reader);
        }
    }

    private List<SearchResult> SearchLikeForCjk(
        string rawQuery,
        int maxResults,
        IReadOnlyCollection<string>? sourceFilter,
        IReadOnlyCollection<string>? excludeSources,
        IReadOnlyCollection<string>? roleFilter)
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            var where = new List<string> { "m.content LIKE $like_query" };
            cmd.Parameters.AddWithValue("$like_query", $"%{rawQuery}%");
            AppendFilters(cmd, where, sourceFilter, excludeSources, roleFilter);
            cmd.Parameters.AddWithValue("$snippet_query", rawQuery);
            cmd.Parameters.AddWithValue("$limit", Math.Clamp(maxResults, 1, 100));
            cmd.CommandText = $@"
                SELECT m.session_id, m.role, m.content,
                       substr(m.content, max(1, instr(m.content, $snippet_query) - 40), 120) AS snippet,
                       0.0 AS rank, m.timestamp, m.tool_name, s.source, s.model, s.started_at
                FROM messages m
                JOIN sessions s ON s.id = m.session_id
                WHERE {string.Join(" AND ", where)}
                ORDER BY m.timestamp DESC
                LIMIT $limit";
            using var reader = cmd.ExecuteReader();
            return ReadSearchResults(reader);
        }
    }

    private static void AppendFilters(
        SqliteCommand cmd,
        List<string> where,
        IReadOnlyCollection<string>? sourceFilter,
        IReadOnlyCollection<string>? excludeSources,
        IReadOnlyCollection<string>? roleFilter)
    {
        AppendInFilter(cmd, where, "s.source", "source", sourceFilter, include: true);
        AppendInFilter(cmd, where, "s.source", "exclude_source", excludeSources, include: false);

        var effectiveRoles = roleFilter is { Count: > 0 }
            ? roleFilter
            : new[] { "user", "assistant" };
        AppendInFilter(cmd, where, "LOWER(m.role)", "role", effectiveRoles.Select(r => r.ToLowerInvariant()).ToArray(), include: true);
    }

    private static void AppendInFilter(
        SqliteCommand cmd,
        List<string> where,
        string column,
        string prefix,
        IReadOnlyCollection<string>? values,
        bool include)
    {
        if (values is null || values.Count == 0)
            return;

        var names = values.Select((value, i) =>
        {
            var name = $"${prefix}_{i}";
            cmd.Parameters.AddWithValue(name, value);
            return name;
        }).ToArray();
        where.Add($"{column} {(include ? "IN" : "NOT IN")} ({string.Join(",", names)})");
    }

    private static List<SearchResult> ReadSearchResults(SqliteDataReader reader)
    {
        var results = new List<SearchResult>();
        while (reader.Read())
        {
            results.Add(new SearchResult
            {
                SessionId = reader.GetString(0),
                Role = reader.GetString(1),
                Content = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Snippet = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Rank = reader.GetDouble(4),
                Timestamp = FromUnixSeconds(reader.GetDouble(5)).ToString("O", CultureInfo.InvariantCulture),
                ToolName = reader.IsDBNull(6) ? null : reader.GetString(6),
                Source = reader.GetString(7),
                Model = reader.IsDBNull(8) ? null : reader.GetString(8),
                SessionStartedAt = FromUnixSeconds(reader.GetDouble(9)).ToString("O", CultureInfo.InvariantCulture)
            });
        }

        return results;
    }

    private static string SanitizeQuery(string query)
    {
        var terms = new List<string>();
        var current = new StringBuilder();

        foreach (var ch in query)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                current.Append(ch);
                continue;
            }

            FlushTerm(current, terms);
        }

        FlushTerm(current, terms);
        return string.Join(' ', terms);
    }

    private static void FlushTerm(StringBuilder current, List<string> terms)
    {
        if (current.Length == 0)
            return;

        var term = current.ToString();
        current.Clear();

        if (IsFtsOperator(term))
            term = term.ToLowerInvariant();

        terms.Add(term);
    }

    private static bool IsFtsOperator(string term)
        => string.Equals(term, "AND", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(term, "OR", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(term, "NOT", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(term, "NEAR", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsCjk(string text)
    {
        foreach (var ch in text)
        {
            var cp = ch;
            if ((cp >= 0x4E00 && cp <= 0x9FFF) ||
                (cp >= 0x3400 && cp <= 0x4DBF) ||
                (cp >= 0x20000 && cp <= 0x2A6DF) ||
                (cp >= 0x3000 && cp <= 0x303F) ||
                (cp >= 0x3040 && cp <= 0x309F) ||
                (cp >= 0x30A0 && cp <= 0x30FF) ||
                (cp >= 0xAC00 && cp <= 0xD7AF))
            {
                return true;
            }
        }

        return false;
    }

    private static double ToUnixSeconds(DateTime timestamp)
    {
        var utc = timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        };
        return new DateTimeOffset(utc).ToUnixTimeMilliseconds() / 1000.0;
    }

    private static DateTime FromUnixSeconds(double value)
        => DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(value * 1000)).UtcDateTime;

    private static List<ToolCall>? DeserializeToolCalls(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<ToolCall>>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static ActivityStatus ParseActivityStatus(string? status)
        => Enum.TryParse<ActivityStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : ActivityStatus.Running;

    public void Dispose()
    {
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };
}

public sealed class SearchResult
{
    public required string SessionId { get; init; }
    public required string Role { get; init; }
    public required string Content { get; init; }
    public required string Snippet { get; init; }
    public required double Rank { get; init; }
    public required string Timestamp { get; init; }
    public string? ToolName { get; init; }
    public string Source { get; init; } = "desktop";
    public string? Model { get; init; }
    public string? SessionStartedAt { get; init; }
}

public sealed record SessionMetadata(
    string Id,
    string Source,
    string? ParentSessionId,
    string? Model);
