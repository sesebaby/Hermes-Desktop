namespace Hermes.Agent.Runtime;

using Hermes.Agent.Game;
using System.Text.Json;
using Microsoft.Data.Sqlite;

public sealed record NpcRuntimePersistedState(
    NpcRuntimeControllerSnapshot Controller,
    NpcRuntimeSessionLeaseSnapshot? LeaseSnapshot);

public sealed class NpcRuntimeStateStore
{
    private readonly string _connectionString;
    private readonly object _gate = new();

    public NpcRuntimeStateStore(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? ".");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Pooling = false
        }.ToString();
        InitializeSchema();
    }

    public Task<NpcRuntimePersistedState> LoadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT event_since, event_sequence, next_wake_at_utc, pending_work_item_json, action_slot_json, lease_json, ingress_work_items_json
                FROM runtime_state
                WHERE id = 1;
                """;
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return Task.FromResult(new NpcRuntimePersistedState(NpcRuntimeControllerSnapshot.Empty, null));

            var eventSince = reader.IsDBNull(0) ? null : reader.GetString(0);
            long? eventSequence = reader.IsDBNull(1) ? null : reader.GetInt64(1);
            DateTime? nextWakeAtUtc = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind);
            var pending = Deserialize<NpcRuntimePendingWorkItemSnapshot>(reader, 3);
            var actionSlot = Deserialize<NpcRuntimeActionSlotSnapshot>(reader, 4);
            var lease = Deserialize<NpcRuntimeSessionLeaseSnapshot>(reader, 5);
            var ingressWorkItems = Deserialize<IReadOnlyList<NpcRuntimeIngressWorkItemSnapshot>>(reader, 6) ?? [];

            return Task.FromResult(new NpcRuntimePersistedState(
                new NpcRuntimeControllerSnapshot(
                    new GameEventCursor(eventSince, eventSequence),
                    pending,
                    actionSlot,
                    nextWakeAtUtc,
                    ingressWorkItems: ingressWorkItems),
                lease));
        }
    }

    public Task SaveAsync(NpcRuntimePersistedState state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            using var db = OpenConnection();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO runtime_state (
                    id,
                    event_since,
                    event_sequence,
                    next_wake_at_utc,
                    pending_work_item_json,
                    action_slot_json,
                    ingress_work_items_json,
                    lease_json,
                    updated_at_utc)
                VALUES (
                    1,
                    $event_since,
                    $event_sequence,
                    $next_wake_at_utc,
                    $pending_work_item_json,
                    $action_slot_json,
                    $ingress_work_items_json,
                    $lease_json,
                    $updated_at_utc)
                ON CONFLICT(id) DO UPDATE SET
                    event_since = excluded.event_since,
                    event_sequence = excluded.event_sequence,
                    next_wake_at_utc = excluded.next_wake_at_utc,
                    pending_work_item_json = excluded.pending_work_item_json,
                    action_slot_json = excluded.action_slot_json,
                    ingress_work_items_json = excluded.ingress_work_items_json,
                    lease_json = excluded.lease_json,
                    updated_at_utc = excluded.updated_at_utc;
                """;
            cmd.Parameters.AddWithValue("$event_since", (object?)state.Controller.EventCursor.Since ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$event_sequence", (object?)state.Controller.EventCursor.Sequence ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$next_wake_at_utc", state.Controller.NextWakeAtUtc?.ToString("O") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$pending_work_item_json", Serialize(state.Controller.PendingWorkItem));
            cmd.Parameters.AddWithValue("$action_slot_json", Serialize(state.Controller.ActionSlot));
            cmd.Parameters.AddWithValue("$ingress_work_items_json", Serialize(state.Controller.IngressWorkItems));
            cmd.Parameters.AddWithValue("$lease_json", Serialize(state.LeaseSnapshot));
            cmd.Parameters.AddWithValue("$updated_at_utc", DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
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
                CREATE TABLE IF NOT EXISTS runtime_state (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    event_since TEXT,
                    event_sequence INTEGER,
                    next_wake_at_utc TEXT,
                    pending_work_item_json TEXT,
                    action_slot_json TEXT,
                    ingress_work_items_json TEXT,
                    lease_json TEXT,
                    updated_at_utc TEXT NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
            EnsureColumn(db, "runtime_state", "ingress_work_items_json", "TEXT");
        }
    }

    private static void EnsureColumn(SqliteConnection db, string tableName, string columnName, string definition)
    {
        using var columns = db.CreateCommand();
        columns.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = columns.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                return;
        }

        using var alter = db.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};";
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
