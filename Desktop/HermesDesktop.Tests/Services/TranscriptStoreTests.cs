using Hermes.Agent.Core;
using Hermes.Agent.Search;
using Hermes.Agent.Transcript;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HermesDesktop.Tests.Services;

/// <summary>
/// Tests for TranscriptStore — the persistence layer used by the new HermesChatService
/// (which replaced the old sidecar-based approach with direct in-process execution).
/// HermesChatService.SendAsync saves every new message via TranscriptStore.SaveMessageAsync.
/// </summary>
[TestClass]
public class TranscriptStoreTests
{
    private string _tempDir = "";

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hermes-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private TranscriptStore CreateStore(bool eagerFlush = false)
        => new(_tempDir, eagerFlush);

    // ── Python state.db parity ──

    [TestMethod]
    public void SessionSearchIndex_Constructor_CreatesPythonStyleStateDbSchema()
    {
        var dbPath = Path.Combine(_tempDir, "state.db");

        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);

        using var db = new SqliteConnection($"Data Source={dbPath}");
        db.Open();
        CollectionAssert.IsSubsetOf(
            new[] { "schema_version", "sessions", "messages", "state_meta", "messages_fts", "activities" },
            ReadTableNames(db));
        Assert.AreEqual(9L, ExecuteScalarLong(db, "SELECT version FROM schema_version LIMIT 1"));
        CollectionAssert.IsSubsetOf(
            new[] { "id", "source", "user_id", "model", "model_config", "system_prompt", "parent_session_id", "started_at", "ended_at", "end_reason", "message_count", "tool_call_count", "api_call_count" },
            ReadColumnNames(db, "sessions"));
        CollectionAssert.IsSubsetOf(
            new[] { "id", "session_id", "role", "content", "tool_call_id", "tool_calls", "tool_name", "timestamp", "token_count", "finish_reason", "reasoning", "reasoning_content", "reasoning_details", "codex_reasoning_items", "codex_message_items" },
            ReadColumnNames(db, "messages"));
    }

    [TestMethod]
    public void SessionSearchIndex_SaveMessage_CreatesSessionRowsAndCounters()
    {
        var dbPath = Path.Combine(_tempDir, "state.db");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        var message = new Message
        {
            Role = "assistant",
            Content = "Will call a tool for python-style state.",
            ToolCalls = new List<ToolCall>
            {
                new() { Id = "call-1", Name = "memory", Arguments = "{\"action\":\"add\"}" }
            }
        };

        index.SaveMessage("sqlite-session", message, source: "desktop");

        using var db = new SqliteConnection($"Data Source={dbPath}");
        db.Open();
        Assert.AreEqual(1L, ExecuteScalarLong(db, "SELECT COUNT(*) FROM sessions WHERE id = 'sqlite-session' AND source = 'desktop'"));
        Assert.AreEqual(1L, ExecuteScalarLong(db, "SELECT message_count FROM sessions WHERE id = 'sqlite-session'"));
        Assert.AreEqual(1L, ExecuteScalarLong(db, "SELECT tool_call_count FROM sessions WHERE id = 'sqlite-session'"));
        Assert.AreEqual(1L, ExecuteScalarLong(db, "SELECT COUNT(*) FROM messages WHERE session_id = 'sqlite-session' AND role = 'assistant'"));
        Assert.IsTrue((ExecuteScalarString(db, "SELECT tool_calls FROM messages WHERE session_id = 'sqlite-session'") ?? "").Contains("call-1"));
    }

    [TestMethod]
    public async Task TranscriptStore_WithSqliteStateStore_LoadsSqliteOnlySessions()
    {
        var dbPath = Path.Combine(_tempDir, "state.db");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        index.SaveMessage("sqlite-only", new Message { Role = "user", Content = "sqlite authoritative orchid-river" }, source: "desktop");
        var store = new TranscriptStore(_tempDir, sessionStore: index);

        Assert.IsTrue(store.SessionExists("sqlite-only"));
        var loaded = await store.LoadSessionAsync("sqlite-only", CancellationToken.None);
        var ids = store.GetAllSessionIds();

        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual("sqlite authoritative orchid-river", loaded[0].Content);
        CollectionAssert.Contains(ids, "sqlite-only");
    }

    [TestMethod]
    public void SessionSearchIndex_Search_SupportsSourceFiltersAndCjkFallback()
    {
        var dbPath = Path.Combine(_tempDir, "state.db");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        index.SaveMessage("desktop-session", new Message { Role = "user", Content = "中文任务 石榴计划" }, source: "desktop");
        index.SaveMessage("gateway-session", new Message { Role = "user", Content = "中文任务 石榴计划" }, source: "discord");
        index.SaveMessage("dot-session", new Message { Role = "user", Content = "Fix my-app.config.ts and alpha-token" }, source: "desktop");

        var cjk = index.Search("石榴计划", maxResults: 5, sourceFilter: new[] { "desktop" });
        var dotted = index.Search("my-app.config.ts", maxResults: 5, excludeSources: new[] { "discord" });

        Assert.AreEqual(1, cjk.Count);
        Assert.AreEqual("desktop-session", cjk[0].SessionId);
        Assert.AreEqual("desktop", cjk[0].Source);
        Assert.IsTrue(dotted.Any(r => r.SessionId == "dot-session"));
    }

    [TestMethod]
    public void SessionSearchIndex_Search_TreatsColonLabelsAsPlainTerms()
    {
        var dbPath = Path.Combine(_tempDir, "state.db");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        index.SaveMessage("npc-session", new Message { Role = "user", Content = "NPC Haley haley is near the town fountain" }, source: "desktop");

        var results = index.Search("NPC: Haley (haley)", maxResults: 5);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("npc-session", results[0].SessionId);
    }

    [TestMethod]
    public void SessionSearchIndex_Search_TreatsSentencePunctuationAsPlainSeparators()
    {
        var dbPath = Path.Combine(_tempDir, "state.db");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        index.SaveMessage("punctuation-session", new Message { Role = "user", Content = "NPC Haley current route valid" }, source: "desktop");

        var results = index.Search("NPC: Haley. current; route=valid", maxResults: 5);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("punctuation-session", results[0].SessionId);
    }

    [TestMethod]
    public async Task TranscriptStore_SaveActivityAsync_WritesStateDbWithoutJsonl()
    {
        var store = CreateStore();
        var entry = new ActivityEntry
        {
            ToolName = "shell",
            ToolCallId = "call-activity",
            InputSummary = "git status",
            OutputSummary = "clean",
            DurationMs = 42,
            Status = ActivityStatus.Success,
            DiffPreview = "diff",
            ScreenshotPath = "shot.png"
        };

        await store.SaveActivityAsync("activity-session", entry, CancellationToken.None);
        var loaded = await store.LoadActivityAsync("activity-session", CancellationToken.None);

        AssertNoJsonlFiles();
        using var db = OpenDefaultStateDb();
        Assert.AreEqual(1L, ExecuteScalarLong(db, "SELECT COUNT(*) FROM activities WHERE session_id = 'activity-session'"));
        Assert.AreEqual("shell", ExecuteScalarString(db, "SELECT tool_name FROM activities WHERE session_id = 'activity-session'"));
        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual("call-activity", loaded[0].ToolCallId);
        Assert.AreEqual(ActivityStatus.Success, loaded[0].Status);
    }

    [TestMethod]
    public async Task Constructor_ImportsLegacyMessageJsonlIntoStateDbAndDeletesJsonl()
    {
        var legacyPath = Path.Combine(_tempDir, "legacy-session.jsonl");
        var legacyMessage = new Message
        {
            Role = "user",
            Content = "legacy import sapphire-ridge",
            Timestamp = new DateTime(2026, 4, 25, 10, 0, 0, DateTimeKind.Utc)
        };
        await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(legacyMessage, JsonOptions) + "\n");

        var store = CreateStore();
        var loaded = await store.LoadSessionAsync("legacy-session", CancellationToken.None);

        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual("legacy import sapphire-ridge", loaded[0].Content);
        AssertNoJsonlFiles();
        AssertNoLegacyPayloadBackups();
    }

    [TestMethod]
    public async Task Constructor_ImportsLegacyActivityJsonlIntoStateDbAndDeletesJsonl()
    {
        var legacyPath = Path.Combine(_tempDir, "legacy-session.activity.jsonl");
        var legacyEntry = new ActivityEntry
        {
            ToolName = "legacy-tool",
            ToolCallId = "legacy-call",
            Status = ActivityStatus.Failed,
            OutputSummary = "legacy output"
        };
        await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(legacyEntry, ActivityJsonOptions) + "\n");

        var store = CreateStore();
        var loaded = await store.LoadActivityAsync("legacy-session", CancellationToken.None);

        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual("legacy-tool", loaded[0].ToolName);
        Assert.AreEqual(ActivityStatus.Failed, loaded[0].Status);
        AssertNoJsonlFiles();
        AssertNoLegacyPayloadBackups();
    }

    [TestMethod]
    public async Task Constructor_DeletesMalformedLegacyJsonlAndRecordsImportErrorMetadata()
    {
        var legacyPath = Path.Combine(_tempDir, "mixed-legacy.jsonl");
        var validMessage = new Message
        {
            Role = "user",
            Content = "valid line survives malformed legacy import"
        };
        await File.WriteAllTextAsync(
            legacyPath,
            JsonSerializer.Serialize(validMessage, JsonOptions) + "\n{not valid json}\n");

        var store = CreateStore();
        var loaded = await store.LoadSessionAsync("mixed-legacy", CancellationToken.None);

        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual("valid line survives malformed legacy import", loaded[0].Content);
        AssertNoJsonlFiles();
        AssertNoLegacyPayloadBackups();
        using var db = OpenDefaultStateDb();
        Assert.AreEqual(1L, ExecuteScalarLong(db, "SELECT COUNT(*) FROM state_meta WHERE key LIKE 'legacy_import_error:mixed-legacy:%'"));
        var meta = ExecuteScalarString(db, "SELECT value FROM state_meta WHERE key LIKE 'legacy_import_error:mixed-legacy:%'");
        StringAssert.Contains(meta ?? "", "JsonException");
        Assert.IsFalse((meta ?? "").Contains("{not valid json}", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetRecentSessionIds_OrdersByLastMessageTimestamp()
    {
        var store = CreateStore();
        await SaveAtAsync(store, "s1", "s1-old", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await SaveAtAsync(store, "s1", "s1-newer", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        await SaveAtAsync(store, "s2", "s2-newest", new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc));
        await SaveAtAsync(store, "s3", "s3-middle", new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        await SaveAtAsync(store, "s4", "s4-oldest", new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        await SaveAtAsync(store, "s5", "s5-recent", new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc));

        var recent = store.GetRecentSessionIds(4);

        CollectionAssert.AreEqual(new[] { "s2", "s5", "s3", "s1" }, recent);
    }

    // ── Construction ──

    [TestMethod]
    public void Constructor_CreatesTranscriptsDirectory_IfNotExists()
    {
        var subDir = Path.Combine(_tempDir, "nested", "transcripts");
        _ = new TranscriptStore(subDir);

        Assert.IsTrue(Directory.Exists(subDir));
    }

    // ── SaveMessageAsync ──

    [TestMethod]
    public async Task SaveMessageAsync_WritesStateDbWithoutSessionJsonl()
    {
        var store = CreateStore();
        var msg = new Message { Role = "user", Content = "Hello there" };

        await store.SaveMessageAsync("session1", msg, CancellationToken.None);

        AssertNoJsonlFiles();
        using var db = OpenDefaultStateDb();
        Assert.AreEqual(1L, ExecuteScalarLong(db, "SELECT COUNT(*) FROM sessions WHERE id = 'session1'"));
        Assert.AreEqual("Hello there", ExecuteScalarString(db, "SELECT content FROM messages WHERE session_id = 'session1'"));
    }

    [TestMethod]
    public async Task SaveMessageAsync_AppendsMultipleMessages_InSameSession()
    {
        var store = CreateStore();
        var msg1 = new Message { Role = "user", Content = "First" };
        var msg2 = new Message { Role = "assistant", Content = "Second" };

        await store.SaveMessageAsync("sess", msg1, CancellationToken.None);
        await store.SaveMessageAsync("sess", msg2, CancellationToken.None);

        AssertNoJsonlFiles();
        using var db = OpenDefaultStateDb();
        Assert.AreEqual(2L, ExecuteScalarLong(db, "SELECT COUNT(*) FROM messages WHERE session_id = 'sess'"));
        Assert.AreEqual(2L, ExecuteScalarLong(db, "SELECT message_count FROM sessions WHERE id = 'sess'"));
    }

    [TestMethod]
    public async Task SaveMessageAsync_NewStoreInstancePreservesExistingSessionHistoryInCache()
    {
        var store1 = CreateStore();
        await store1.SaveMessageAsync("cross-instance", new Message { Role = "user", Content = "remember my name" }, CancellationToken.None);
        await store1.SaveMessageAsync("cross-instance", new Message { Role = "assistant", Content = "I will remember it" }, CancellationToken.None);

        var store2 = CreateStore();
        await store2.SaveMessageAsync("cross-instance", new Message { Role = "user", Content = "what is my name?" }, CancellationToken.None);

        var loaded = await store2.LoadSessionAsync("cross-instance", CancellationToken.None);

        Assert.AreEqual(3, loaded.Count);
        Assert.AreEqual("remember my name", loaded[0].Content);
        Assert.AreEqual("I will remember it", loaded[1].Content);
        Assert.AreEqual("what is my name?", loaded[2].Content);
    }

    [TestMethod]
    public async Task SaveMessageAsync_DifferentSessions_CreateSeparateSessionRows()
    {
        var store = CreateStore();

        await store.SaveMessageAsync("session-a", new Message { Role = "user", Content = "A" }, CancellationToken.None);
        await store.SaveMessageAsync("session-b", new Message { Role = "user", Content = "B" }, CancellationToken.None);

        AssertNoJsonlFiles();
        using var db = OpenDefaultStateDb();
        Assert.AreEqual(2L, ExecuteScalarLong(db, "SELECT COUNT(*) FROM sessions"));
    }

    [TestMethod]
    public async Task SaveMessageAsync_WithEagerFlush_StillPersists()
    {
        var store = CreateStore(eagerFlush: true);
        var msg = new Message { Role = "user", Content = "eager" };

        await store.SaveMessageAsync("eager-sess", msg, CancellationToken.None);

        Assert.IsTrue(store.SessionExists("eager-sess"));
    }

    // ── LoadSessionAsync ──

    [TestMethod]
    public async Task LoadSessionAsync_ReturnsAllSavedMessages_InOrder()
    {
        var store = CreateStore();
        var messages = new[]
        {
            new Message { Role = "user", Content = "msg1" },
            new Message { Role = "assistant", Content = "msg2" },
            new Message { Role = "user", Content = "msg3" },
        };

        foreach (var m in messages)
            await store.SaveMessageAsync("s1", m, CancellationToken.None);

        var loaded = await store.LoadSessionAsync("s1", CancellationToken.None);

        Assert.AreEqual(3, loaded.Count);
        Assert.AreEqual("msg1", loaded[0].Content);
        Assert.AreEqual("msg2", loaded[1].Content);
        Assert.AreEqual("msg3", loaded[2].Content);
    }

    [TestMethod]
    public async Task LoadSessionAsync_PreservesRoles()
    {
        var store = CreateStore();
        await store.SaveMessageAsync("s2", new Message { Role = "user", Content = "hi" }, CancellationToken.None);
        await store.SaveMessageAsync("s2", new Message { Role = "assistant", Content = "hey" }, CancellationToken.None);
        await store.SaveMessageAsync("s2", new Message { Role = "tool", Content = "result", ToolCallId = "c1", ToolName = "todo_write" }, CancellationToken.None);

        var loaded = await store.LoadSessionAsync("s2", CancellationToken.None);

        Assert.AreEqual("user", loaded[0].Role);
        Assert.AreEqual("assistant", loaded[1].Role);
        Assert.AreEqual("tool", loaded[2].Role);
        Assert.AreEqual("c1", loaded[2].ToolCallId);
        Assert.AreEqual("todo_write", loaded[2].ToolName);
    }

    [TestMethod]
    public async Task LoadSessionAsync_ThrowsSessionNotFoundException_ForUnknownSession()
    {
        var store = CreateStore();

        await Assert.ThrowsExceptionAsync<SessionNotFoundException>(async () =>
            await store.LoadSessionAsync("nonexistent-id", CancellationToken.None));
    }

    [TestMethod]
    public async Task LoadSessionAsync_ReturnsCachedResult_OnSecondCall()
    {
        var store = CreateStore();
        await store.SaveMessageAsync("s3", new Message { Role = "user", Content = "cached" }, CancellationToken.None);

        var first = await store.LoadSessionAsync("s3", CancellationToken.None);
        var second = await store.LoadSessionAsync("s3", CancellationToken.None);

        // Both should return the same data
        Assert.AreEqual(first.Count, second.Count);
        Assert.AreEqual(first[0].Content, second[0].Content);
    }

    [TestMethod]
    public async Task LoadSessionAsync_ReturnsNewListInstance_EachCall()
    {
        var store = CreateStore();
        await store.SaveMessageAsync("s4", new Message { Role = "user", Content = "x" }, CancellationToken.None);

        var first = await store.LoadSessionAsync("s4", CancellationToken.None);
        var second = await store.LoadSessionAsync("s4", CancellationToken.None);

        // Modifying one list should not affect the other (returns a copy from cache)
        first.Add(new Message { Role = "user", Content = "injected" });
        Assert.AreEqual(1, second.Count, "Second load should not see mutation of first load result");
    }

    // ── SessionExists ──

    [TestMethod]
    public async Task SessionExists_ReturnsFalse_ForUnknownSession()
    {
        var store = CreateStore();

        Assert.IsFalse(store.SessionExists("ghost"));
    }

    [TestMethod]
    public async Task SessionExists_ReturnsTrue_AfterSave()
    {
        var store = CreateStore();
        await store.SaveMessageAsync("known", new Message { Role = "user", Content = "x" }, CancellationToken.None);

        Assert.IsTrue(store.SessionExists("known"));
    }

    [TestMethod]
    public async Task SessionExists_ReturnsTrue_FromDiskWithoutCache()
    {
        // Save in one store instance (written to disk)
        var store1 = CreateStore();
        await store1.SaveMessageAsync("disk-sess", new Message { Role = "user", Content = "y" }, CancellationToken.None);

        // New store instance has empty cache but same directory
        var store2 = CreateStore();
        Assert.IsTrue(store2.SessionExists("disk-sess"), "Should detect session from disk even with empty cache");
    }

    // ── GetAllSessionIds ──

    [TestMethod]
    public void GetAllSessionIds_ReturnsEmpty_WhenNoSessions()
    {
        var store = CreateStore();

        var ids = store.GetAllSessionIds();

        Assert.AreEqual(0, ids.Count);
    }

    [TestMethod]
    public async Task GetAllSessionIds_ReturnsAllSessionIds()
    {
        var store = CreateStore();
        await store.SaveMessageAsync("alpha", new Message { Role = "user", Content = "a" }, CancellationToken.None);
        await store.SaveMessageAsync("beta", new Message { Role = "user", Content = "b" }, CancellationToken.None);
        await store.SaveMessageAsync("gamma", new Message { Role = "user", Content = "c" }, CancellationToken.None);

        var ids = store.GetAllSessionIds();

        Assert.AreEqual(3, ids.Count);
        CollectionAssert.Contains(ids, "alpha");
        CollectionAssert.Contains(ids, "beta");
        CollectionAssert.Contains(ids, "gamma");
    }

    [TestMethod]
    public async Task GetAllSessionIds_IncludesFromDisk_NotJustCache()
    {
        // Write to disk via one store
        var store1 = CreateStore();
        await store1.SaveMessageAsync("disk-only", new Message { Role = "user", Content = "z" }, CancellationToken.None);

        // New store has empty cache
        var store2 = CreateStore();
        var ids = store2.GetAllSessionIds();

        CollectionAssert.Contains(ids, "disk-only");
    }

    [TestMethod]
    public async Task GetAllSessionIds_DeduplicatesCacheAndDisk()
    {
        // Write to disk AND cache by loading
        var store = CreateStore();
        await store.SaveMessageAsync("both", new Message { Role = "user", Content = "q" }, CancellationToken.None);
        await store.LoadSessionAsync("both", CancellationToken.None); // populates cache

        var ids = store.GetAllSessionIds();

        var count = ids.Count(id => id == "both");
        Assert.AreEqual(1, count, "Same session should not appear twice");
    }

    // ── DeleteSessionAsync ──

    [TestMethod]
    public async Task DeleteSessionAsync_RemovesRows_FromStateDb()
    {
        var store = CreateStore();
        await store.SaveMessageAsync("to-delete", new Message { Role = "user", Content = "bye" }, CancellationToken.None);
        await store.SaveActivityAsync("to-delete", new ActivityEntry { ToolName = "shell" }, CancellationToken.None);

        await store.DeleteSessionAsync("to-delete", CancellationToken.None);

        Assert.IsFalse(store.SessionExists("to-delete"));
        using var db = OpenDefaultStateDb();
        Assert.AreEqual(0L, ExecuteScalarLong(db, "SELECT COUNT(*) FROM sessions WHERE id = 'to-delete'"));
        Assert.AreEqual(0L, ExecuteScalarLong(db, "SELECT COUNT(*) FROM messages WHERE session_id = 'to-delete'"));
        Assert.AreEqual(0L, ExecuteScalarLong(db, "SELECT COUNT(*) FROM activities WHERE session_id = 'to-delete'"));
    }

    [TestMethod]
    public async Task DeleteSessionAsync_ForNonExistentSession_DoesNotThrow()
    {
        var store = CreateStore();

        // Should complete without exception
        await store.DeleteSessionAsync("ghost-session", CancellationToken.None);
    }

    [TestMethod]
    public async Task DeleteSessionAsync_RemovesFromGetAllSessionIds()
    {
        var store = CreateStore();
        await store.SaveMessageAsync("del-test", new Message { Role = "user", Content = "x" }, CancellationToken.None);

        await store.DeleteSessionAsync("del-test", CancellationToken.None);
        var ids = store.GetAllSessionIds();

        CollectionAssert.DoesNotContain(ids, "del-test");
    }

    // ── ClearCache ──

    [TestMethod]
    public async Task ClearCache_KeepsDataOnDisk_ButEmptiesCache()
    {
        var store = CreateStore();
        await store.SaveMessageAsync("persist", new Message { Role = "user", Content = "keep" }, CancellationToken.None);
        await store.LoadSessionAsync("persist", CancellationToken.None); // populate cache

        store.ClearCache();

        // Data should still be loadable from disk
        var loaded = await store.LoadSessionAsync("persist", CancellationToken.None);
        Assert.AreEqual(1, loaded.Count);
    }

    // ── SessionNotFoundException ──

    [TestMethod]
    public void SessionNotFoundException_ContainsSessionId_InMessage()
    {
        var ex = new SessionNotFoundException("my-session-id");

        StringAssert.Contains(ex.Message, "my-session-id");
    }

    // ── Edge cases (regression) ──

    [TestMethod]
    public async Task SaveAndLoad_MessageWithSpecialCharacters_RoundTrips()
    {
        var store = CreateStore();
        var content = "Hello \"world\"!\nNew line\t tab\r\nWindows line ending";
        await store.SaveMessageAsync("special", new Message { Role = "user", Content = content }, CancellationToken.None);

        var loaded = await store.LoadSessionAsync("special", CancellationToken.None);

        Assert.AreEqual(content, loaded[0].Content);
    }

    [TestMethod]
    public async Task SaveAndLoad_MessageWithUnicodeContent_RoundTrips()
    {
        var store = CreateStore();
        var content = "日本語テスト 🎉 emoji ñoño";
        await store.SaveMessageAsync("unicode", new Message { Role = "user", Content = content }, CancellationToken.None);

        var loaded = await store.LoadSessionAsync("unicode", CancellationToken.None);

        Assert.AreEqual(content, loaded[0].Content);
    }

    [TestMethod]
    public async Task SaveAndLoad_EmptyContent_RoundTrips()
    {
        var store = CreateStore();
        await store.SaveMessageAsync("empty", new Message { Role = "system", Content = "" }, CancellationToken.None);

        var loaded = await store.LoadSessionAsync("empty", CancellationToken.None);

        Assert.AreEqual("", loaded[0].Content);
    }

    [TestMethod]
    public async Task ConcurrentSaves_ToSameSession_AllMessagesPreserved()
    {
        var store = CreateStore();
        const int count = 20;

        var tasks = Enumerable.Range(0, count).Select(i =>
            store.SaveMessageAsync("concurrent", new Message { Role = "user", Content = $"msg-{i}" }, CancellationToken.None));

        await Task.WhenAll(tasks);

        var loaded = await store.LoadSessionAsync("concurrent", CancellationToken.None);
        Assert.AreEqual(count, loaded.Count, "All concurrent saves should be present");
    }

    private static string[] ReadTableNames(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type IN ('table', 'virtual table') ORDER BY name";
        using var reader = cmd.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names.ToArray();
    }

    private SqliteConnection OpenDefaultStateDb()
    {
        var db = new SqliteConnection($"Data Source={Path.Combine(_tempDir, "state.db")}");
        db.Open();
        return db;
    }

    private void AssertNoJsonlFiles()
    {
        var files = Directory.GetFiles(_tempDir, "*.jsonl", SearchOption.AllDirectories);
        Assert.AreEqual(0, files.Length, "Session and activity data must be stored in state.db, not JSONL files.");
    }

    private void AssertNoLegacyPayloadBackups()
    {
        var backups = Directory.GetFiles(_tempDir, "*.imported", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_tempDir, "*.unimported", SearchOption.AllDirectories))
            .ToArray();
        Assert.AreEqual(0, backups.Length, "Legacy JSONL payloads must not be retained under alternate extensions.");
    }

    private static Task SaveAtAsync(TranscriptStore store, string sessionId, string content, DateTime timestamp)
        => store.SaveMessageAsync(
            sessionId,
            new Message { Role = "user", Content = content, Timestamp = timestamp },
            CancellationToken.None);

    private static string[] ReadColumnNames(SqliteConnection db, string table)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var reader = cmd.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(1));
        return names.ToArray();
    }

    private static long ExecuteScalarLong(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    private static string? ExecuteScalarString(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar() as string;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions ActivityJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };
}
