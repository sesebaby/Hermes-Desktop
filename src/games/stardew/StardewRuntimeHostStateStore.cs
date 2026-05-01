namespace Hermes.Agent.Games.Stardew;

using System.Text.Json;
using Hermes.Agent.Game;
using Microsoft.Data.Sqlite;

public sealed record StardewRuntimeHostStagedBatch(
    IReadOnlyList<GameEventRecord> Records,
    GameEventCursor NextCursor,
    bool PrivateChatDrainOnly)
{
    public GameEventBatch ToBatch()
        => new(Records, NextCursor);
}

public sealed record StardewRuntimeHostState(
    GameEventCursor SourceCursor,
    StardewRuntimeHostStagedBatch? StagedBatch,
    bool InitialPrivateChatHistoryDrained)
{
    public static StardewRuntimeHostState Empty { get; } = new(new GameEventCursor(), null, false);
}

public sealed class StardewRuntimeHostStateStore
{
    private readonly string _connectionString;
    private readonly object _gate = new();

    public StardewRuntimeHostStateStore(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? ".");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Pooling = false
        }.ToString();
        InitializeSchema();
    }

    public Task<StardewRuntimeHostState> LoadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT source_cursor_since,
                       source_cursor_sequence,
                       staged_records_json,
                       staged_next_cursor_since,
                       staged_next_cursor_sequence,
                       staged_private_chat_drain_only,
                       initial_private_chat_history_drained
                FROM host_state
                WHERE id = 1;
                """;

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return Task.FromResult(StardewRuntimeHostState.Empty);

            var sourceCursor = new GameEventCursor(
                reader.IsDBNull(0) ? null : reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetInt64(1));
            var records = Deserialize<GameEventRecord[]>(reader, 2);
            var initialPrivateChatHistoryDrained = !reader.IsDBNull(6) && reader.GetBoolean(6);
            StardewRuntimeHostStagedBatch? stagedBatch = null;
            if (records is { Length: > 0 } || !reader.IsDBNull(3) || !reader.IsDBNull(4) || (!reader.IsDBNull(5) && reader.GetBoolean(5)))
            {
                stagedBatch = new StardewRuntimeHostStagedBatch(
                    records ?? Array.Empty<GameEventRecord>(),
                    new GameEventCursor(
                        reader.IsDBNull(3) ? null : reader.GetString(3),
                        reader.IsDBNull(4) ? null : reader.GetInt64(4)),
                    !reader.IsDBNull(5) && reader.GetBoolean(5));
            }

            return Task.FromResult(new StardewRuntimeHostState(sourceCursor, stagedBatch, initialPrivateChatHistoryDrained));
        }
    }

    public Task StageBatchAsync(
        GameEventCursor sourceCursor,
        GameEventBatch batch,
        bool privateChatDrainOnly,
        bool initialPrivateChatHistoryDrained,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceCursor);
        ArgumentNullException.ThrowIfNull(batch);
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            using var db = OpenConnection();
            UpsertState(
                db,
                sourceCursor,
                batch.Records,
                batch.NextCursor,
                privateChatDrainOnly,
                initialPrivateChatHistoryDrained);
        }

        return Task.CompletedTask;
    }

    public Task CommitBatchAsync(GameEventCursor nextCursor, bool initialPrivateChatHistoryDrained, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(nextCursor);
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            using var db = OpenConnection();
            UpsertState(
                db,
                nextCursor,
                records: null,
                stagedNextCursor: null,
                stagedPrivateChatDrainOnly: false,
                initialPrivateChatHistoryDrained: initialPrivateChatHistoryDrained);
        }

        return Task.CompletedTask;
    }

    private SqliteConnection OpenConnection()
    {
        var db = new SqliteConnection(_connectionString);
        db.Open();
        using var pragma = db.CreateCommand();
        pragma.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;
            """;
        pragma.ExecuteNonQuery();
        return db;
    }

    private void InitializeSchema()
    {
        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS host_state (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    source_cursor_since TEXT,
                    source_cursor_sequence INTEGER,
                    staged_records_json TEXT,
                    staged_next_cursor_since TEXT,
                    staged_next_cursor_sequence INTEGER,
                    staged_private_chat_drain_only INTEGER NOT NULL DEFAULT 0,
                    initial_private_chat_history_drained INTEGER NOT NULL DEFAULT 0,
                    updated_at_utc TEXT NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
            EnsureColumn(db, "host_state", "staged_private_chat_drain_only", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(db, "host_state", "initial_private_chat_history_drained", "INTEGER NOT NULL DEFAULT 0");
        }
    }

    private static void UpsertState(
        SqliteConnection db,
        GameEventCursor sourceCursor,
        IReadOnlyList<GameEventRecord>? records,
        GameEventCursor? stagedNextCursor,
        bool stagedPrivateChatDrainOnly,
        bool initialPrivateChatHistoryDrained)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO host_state (
                id,
                source_cursor_since,
                source_cursor_sequence,
                staged_records_json,
                staged_next_cursor_since,
                staged_next_cursor_sequence,
                staged_private_chat_drain_only,
                initial_private_chat_history_drained,
                updated_at_utc)
            VALUES (
                1,
                $source_cursor_since,
                $source_cursor_sequence,
                $staged_records_json,
                $staged_next_cursor_since,
                $staged_next_cursor_sequence,
                $staged_private_chat_drain_only,
                $initial_private_chat_history_drained,
                $updated_at_utc)
            ON CONFLICT(id) DO UPDATE SET
                source_cursor_since = excluded.source_cursor_since,
                source_cursor_sequence = excluded.source_cursor_sequence,
                staged_records_json = excluded.staged_records_json,
                staged_next_cursor_since = excluded.staged_next_cursor_since,
                staged_next_cursor_sequence = excluded.staged_next_cursor_sequence,
                staged_private_chat_drain_only = excluded.staged_private_chat_drain_only,
                initial_private_chat_history_drained = excluded.initial_private_chat_history_drained,
                updated_at_utc = excluded.updated_at_utc;
            """;
        cmd.Parameters.AddWithValue("$source_cursor_since", (object?)sourceCursor.Since ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$source_cursor_sequence", (object?)sourceCursor.Sequence ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$staged_records_json", Serialize(records));
        cmd.Parameters.AddWithValue("$staged_next_cursor_since", (object?)stagedNextCursor?.Since ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$staged_next_cursor_sequence", (object?)stagedNextCursor?.Sequence ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$staged_private_chat_drain_only", stagedPrivateChatDrainOnly);
        cmd.Parameters.AddWithValue("$initial_private_chat_history_drained", initialPrivateChatHistoryDrained);
        cmd.Parameters.AddWithValue("$updated_at_utc", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    private static void EnsureColumn(SqliteConnection db, string tableName, string columnName, string columnDefinition)
    {
        using var info = db.CreateCommand();
        info.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = info.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                return;
        }

        using var alter = db.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        alter.ExecuteNonQuery();
    }

    private static string Serialize<T>(T value)
        => value is null ? string.Empty : JsonSerializer.Serialize(value);

    private static T? Deserialize<T>(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return default;

        var json = reader.GetString(ordinal);
        return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json);
    }
}
