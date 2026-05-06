using Hermes.Agent.Context;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Memory;
using Hermes.Agent.Plugins;
using Hermes.Agent.Search;
using Hermes.Agent.Tools;
using Hermes.Agent.Transcript;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace HermesDesktop.Tests.Services;

[TestClass]
public sealed class MemoryParityTests
{
    private string _tempDir = "";

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hermes-memory-parity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task ChatAsync_NoTools_FirstCallAugmentsCurrentUserWithPriorTranscriptRecall()
    {
        var transcripts = await CreateTranscriptsWithPriorMemoryAsync("prior-a", "The user asked me to remember codename basalt-lantern.");
        var client = new RecordingChatClient { CompleteResponse = "I recall basalt-lantern." };
        var agent = CreateAgent(client, transcripts);
        var session = new Session { Id = "current-no-tools" };

        await agent.ChatAsync("What did I ask you to remember before?", session, CancellationToken.None);

        var sent = AssertSingleCompleteCall(client);
        var lastUser = sent.Last(m => m.Role == "user");
        StringAssert.Contains(lastUser.Content, "What did I ask you to remember before?");
        StringAssert.Contains(lastUser.Content, "basalt-lantern");
        StringAssert.Contains(lastUser.Content, "recalled memory context");
        Assert.IsFalse(sent.Any(m => m.Role == "system" && m.Content.Contains("basalt-lantern", StringComparison.OrdinalIgnoreCase)),
            "Transcript recall must not be emitted as a synthetic system message.");

        var persisted = await transcripts.LoadSessionAsync("current-no-tools", CancellationToken.None);
        Assert.AreEqual("What did I ask you to remember before?", persisted[0].Content,
            "Injected recall must not be persisted as user-authored transcript content.");
    }

    [TestMethod]
    public async Task ChatAsync_WithTools_FirstToolLoopCallAugmentsCurrentUserWithPriorTranscriptRecall()
    {
        var transcripts = await CreateTranscriptsWithPriorMemoryAsync("prior-tool", "The earlier task used passphrase copper-harbor.");
        var client = new RecordingChatClient();
        client.ToolResponses.Enqueue(new ChatResponse { Content = "done", FinishReason = "stop" });

        var agent = CreateAgent(client, transcripts);
        agent.RegisterTool(new NoopTool());

        await agent.ChatAsync("What passphrase did we use earlier?", new Session { Id = "current-tools" }, CancellationToken.None);

        Assert.AreEqual(1, client.CompleteWithToolsCalls.Count);
        var lastUser = client.CompleteWithToolsCalls[0].Last(m => m.Role == "user");
        StringAssert.Contains(lastUser.Content, "What passphrase did we use earlier?");
        StringAssert.Contains(lastUser.Content, "copper-harbor");
        StringAssert.Contains(lastUser.Content, "recalled memory context");
    }

    [TestMethod]
    public async Task StreamChatAsync_WithTools_FirstToolLoopCallAugmentsCurrentUserWithPriorTranscriptRecall()
    {
        var transcripts = await CreateTranscriptsWithPriorMemoryAsync("prior-stream", "The previous distinctive marker was glacier-orchid.");
        var client = new RecordingChatClient();
        client.ToolResponses.Enqueue(new ChatResponse { Content = "stream done", FinishReason = "stop" });

        var agent = CreateAgent(client, transcripts);
        agent.RegisterTool(new NoopTool());

        await foreach (var _ in agent.StreamChatAsync("What was the previous marker?", new Session { Id = "current-stream" }, CancellationToken.None))
        {
        }

        Assert.AreEqual(1, client.CompleteWithToolsCalls.Count);
        var lastUser = client.CompleteWithToolsCalls[0].Last(m => m.Role == "user");
        StringAssert.Contains(lastUser.Content, "What was the previous marker?");
        StringAssert.Contains(lastUser.Content, "glacier-orchid");
        StringAssert.Contains(lastUser.Content, "recalled memory context");
    }

    [TestMethod]
    public void PromptBuilder_DoesNotEmitRetrievedTranscriptRecallAsSystemLayer()
    {
        var builder = new PromptBuilder("stable system");
        var packet = new PromptPacket
        {
            SystemPrompt = "stable system",
            SessionStateJson = "{}",
            RetrievedContext = new List<string> { "transcript recall: basalt-lantern" },
            RecentTurns = new List<Message>(),
            CurrentUserMessage = "current question"
        };

        var messages = builder.ToOpenAiMessages(packet);

        Assert.IsFalse(messages.Any(m => m.Role == "system" && m.Content.Contains("basalt-lantern", StringComparison.OrdinalIgnoreCase)),
            "Transcript recall is coordinator-internal transport and must not be surfaced by PromptBuilder as a system layer.");
        Assert.AreEqual("current question", messages.Last().Content);
    }

    [TestMethod]
    public void TurnMemoryCoordinator_BuildMemoryContextBlock_UsesPythonFenceShape()
    {
        var block = TurnMemoryCoordinator.BuildMemoryContextBlock("durable marker basalt-lantern");

        Assert.IsTrue(block.StartsWith("<memory-context>\n[System note:", StringComparison.Ordinal), block);
        StringAssert.Contains(block, "NOT new user input");
        StringAssert.Contains(block, "durable marker basalt-lantern");
        Assert.IsTrue(block.EndsWith("</memory-context>", StringComparison.Ordinal), block);
        Assert.IsFalse(block.Contains("<memory_context>", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task TranscriptStore_SaveMessageAsync_SucceedsWhenIndexObserverThrows()
    {
        var observer = new ThrowingTranscriptObserver();
        var store = new TranscriptStore(_tempDir, messageObserver: observer);

        await store.SaveMessageAsync("observer-session", new Message { Role = "user", Content = "still persists" }, CancellationToken.None);

        Assert.IsTrue(store.SessionExists("observer-session"));
        var loaded = await store.LoadSessionAsync("observer-session", CancellationToken.None);
        Assert.AreEqual("still persists", loaded[0].Content);
        Assert.AreEqual(1, observer.Attempts);
    }

    [TestMethod]
    public async Task TranscriptStore_SaveMessageAsync_IndexesNewMessagesThroughObserver()
    {
        var dbPath = Path.Combine(_tempDir, "state.db");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        var observer = new SessionSearchTranscriptObserver(index, NullLogger<SessionSearchTranscriptObserver>.Instance);
        var store = new TranscriptStore(_tempDir, messageObserver: observer);

        await store.SaveMessageAsync("indexed-session", new Message { Role = "user", Content = "fresh searchable token nebula-cedar" }, CancellationToken.None);

        var results = index.Search("nebula-cedar", maxResults: 5);
        Assert.IsTrue(results.Any(r => r.SessionId == "indexed-session"),
            "New transcript writes should become searchable through the post-write observer.");
    }

    [TestMethod]
    public async Task TranscriptRecallService_BackfillIndexAsync_PopulatesFtsFromExistingSqliteSessions()
    {
        var store = new TranscriptStore(_tempDir);
        await store.SaveMessageAsync("backfill-session", new Message { Role = "user", Content = "legacy transcript contains quartz-meadow" }, CancellationToken.None);

        var dbPath = Path.Combine(_tempDir, "backfill.sqlite");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance, index);

        await recall.BackfillIndexAsync(CancellationToken.None);

        var results = index.Search("quartz-meadow", maxResults: 5);
        Assert.IsTrue(results.Any(r => r.SessionId == "backfill-session"),
            "Existing SQLite sessions should be backfillable into another FTS/state index.");
    }

    [TestMethod]
    public async Task TranscriptRecallService_SearchAsync_UsesExistingFtsIndexWhenAvailable()
    {
        var store = new TranscriptStore(_tempDir);
        var dbPath = Path.Combine(_tempDir, "existing-index.sqlite");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        index.IndexMessage("indexed-only-session", "user", "indexed-only token ember-fjord", DateTime.UtcNow);
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance, index);

        var results = await recall.SearchAsync("ember-fjord", currentSessionId: null, maxItems: 5, ct: CancellationToken.None);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("indexed-only-session", results[0].SessionId);
        StringAssert.Contains(results[0].Content, "ember-fjord");
    }

    [TestMethod]
    public async Task TranscriptRecallService_SearchAsync_DoesNotUseFallbackWhenFtsReturnsHits()
    {
        var store = new TranscriptStore(_tempDir);
        await store.SaveMessageAsync("sqlite-fallback-session", new Message
        {
            Role = "user",
            Content = "sqlite fallback garnet-brook"
        }, CancellationToken.None);
        var dbPath = Path.Combine(_tempDir, "fts-hit.sqlite");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        index.IndexMessage("fts-session", "user", "indexed garnet-brook", DateTime.UtcNow);
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance, index);

        var results = await recall.SearchAsync("garnet-brook", currentSessionId: null, maxItems: 5, ct: CancellationToken.None);

        Assert.AreEqual(1, results.Count,
            "Once FTS returns ranked hits, search must not widen the result set with non-FTS fallback scoring.");
        Assert.AreEqual("fts-session", results[0].SessionId);
        Assert.IsFalse(results.Any(r => r.SessionId == "sqlite-fallback-session"));
    }

    [TestMethod]
    public async Task TranscriptRecallService_IndexBackedSearchExcludesToolMessages()
    {
        var dbPath = Path.Combine(_tempDir, "tool-filter.sqlite");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        var observer = new SessionSearchTranscriptObserver(index, NullLogger<SessionSearchTranscriptObserver>.Instance);
        var store = new TranscriptStore(_tempDir, messageObserver: observer);
        await store.SaveMessageAsync("tool-session", new Message
        {
            Role = "tool",
            Content = "tool-only secret opal-canyon"
        }, CancellationToken.None);
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance, index);

        var results = await recall.SearchAsync("opal-canyon", currentSessionId: null, maxItems: 5, ct: CancellationToken.None);

        Assert.AreEqual(0, results.Count,
            "Index-backed live search must not widen recall beyond user/assistant messages.");
    }

    [TestMethod]
    public async Task TranscriptRecallService_SearchAsync_FallsBackToSqliteWhenIndexHasNoHits()
    {
        var store = new TranscriptStore(_tempDir);
        await store.SaveMessageAsync("fallback-session", new Message
        {
            Role = "user",
            Content = "fallback-only amber-hill"
        }, CancellationToken.None);
        var dbPath = Path.Combine(_tempDir, "stale-index.sqlite");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        index.IndexMessage("unrelated-session", "user", "unrelated indexed content", DateTime.UtcNow);
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance, index);

        var results = await recall.SearchAsync("amber-hill", currentSessionId: null, maxItems: 5, ct: CancellationToken.None);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("fallback-session", results[0].SessionId);
        StringAssert.Contains(results[0].Content, "amber-hill");
    }

    [TestMethod]
    public async Task TranscriptRecallService_SearchAsync_RebuildsPollutedIndexBeforeSearching()
    {
        var store = new TranscriptStore(_tempDir);
        await store.SaveMessageAsync("clean-session", new Message
        {
            Role = "user",
            Content = "clean user sapphire-glen"
        }, CancellationToken.None);
        var dbPath = Path.Combine(_tempDir, "polluted-index.sqlite");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        index.IndexMessage("polluted-session", "tool", "polluted tool sapphire-glen", DateTime.UtcNow);
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance, index);

        var results = await recall.SearchAsync("sapphire-glen", currentSessionId: null, maxItems: 5, ct: CancellationToken.None);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("clean-session", results[0].SessionId);
        StringAssert.Contains(results[0].Content, "clean user sapphire-glen");
        Assert.IsFalse(results[0].Content.Contains("polluted tool", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task TranscriptRecallService_SearchSessionSummariesAsync_ExcludesToolMessagesFromSummaryInput()
    {
        var store = new TranscriptStore(_tempDir);
        await store.SaveMessageAsync("summary-tool-session", new Message
        {
            Role = "user",
            Content = "Please remember topaz-reef"
        }, CancellationToken.None);
        await store.SaveMessageAsync("summary-tool-session", new Message
        {
            Role = "tool",
            ToolName = "todo",
            Content = "TOOL SECRET should not surface in summary"
        }, CancellationToken.None);
        await store.SaveMessageAsync("summary-tool-session", new Message
        {
            Role = "assistant",
            Content = "The visible conclusion is topaz-reef"
        }, CancellationToken.None);
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance);

        var summaries = await recall.SearchSessionSummariesAsync("topaz-reef", maxSessions: 1, ct: CancellationToken.None);

        Assert.AreEqual(1, summaries.Count);
        StringAssert.Contains(summaries[0].Summary, "topaz-reef");
        Assert.IsFalse(summaries[0].Summary.Contains("TOOL SECRET", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(summaries[0].Summary.Contains("TOOL(", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task TranscriptRecallService_SearchSessionSummariesAsync_ExcludesCurrentSessionLineage()
    {
        var dbPath = Path.Combine(_tempDir, "lineage.sqlite");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        var store = new TranscriptStore(_tempDir, sessionStore: index);
        var now = DateTime.UtcNow;
        index.SaveMessage("current-root", new Message { Role = "user", Content = "lineage-token current root", Timestamp = now }, parentSessionId: null);
        index.SaveMessage("current-child", new Message { Role = "user", Content = "lineage-token current child", Timestamp = now.AddSeconds(1) }, parentSessionId: "current-root");
        index.SaveMessage("other-root", new Message { Role = "user", Content = "lineage-token other root", Timestamp = now.AddSeconds(2) }, parentSessionId: null);
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance, index);

        var fromChild = await recall.SearchSessionSummariesAsync("lineage-token", "current-child", maxSessions: 5, ct: CancellationToken.None);
        var fromRoot = await recall.SearchSessionSummariesAsync("lineage-token", "current-root", maxSessions: 5, ct: CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "other-root" }, fromChild.Select(s => s.SessionId).ToArray(),
            "Searching from a compressed/delegated child session must exclude its root lineage.");
        CollectionAssert.AreEqual(new[] { "other-root" }, fromRoot.Select(s => s.SessionId).ToArray(),
            "Searching from a root session must exclude child sessions that resolve back to the current root.");
    }

    [TestMethod]
    public async Task TranscriptRecallService_SearchAsync_ExcludesHiddenToolSourceSessions()
    {
        var dbPath = Path.Combine(_tempDir, "hidden-source.sqlite");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        var store = new TranscriptStore(_tempDir, sessionStore: index);
        index.SaveMessage("visible-session", new Message { Role = "user", Content = "source-token visible" }, source: "desktop");
        index.SaveMessage("hidden-tool-session", new Message { Role = "user", Content = "source-token hidden" }, source: "tool");
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance, index);

        var results = await recall.SearchAsync("source-token", currentSessionId: null, maxItems: 5, ct: CancellationToken.None);

        Assert.IsTrue(results.Any(r => r.SessionId == "visible-session"));
        Assert.IsFalse(results.Any(r => r.SessionId == "hidden-tool-session"),
            "Python hides sessions whose source is 'tool' from session_search by default.");
    }

    [TestMethod]
    public async Task TranscriptRecallService_ListRecentSessions_ExcludesHiddenSourcesAndChildSessions()
    {
        var dbPath = Path.Combine(_tempDir, "recent-filter.sqlite");
        using var index = new SessionSearchIndex(dbPath, NullLogger<SessionSearchIndex>.Instance);
        var store = new TranscriptStore(_tempDir, sessionStore: index);
        var now = DateTime.UtcNow;
        index.SaveMessage("visible-root", new Message { Role = "user", Content = "visible recent", Timestamp = now }, source: "desktop");
        index.SaveMessage("visible-child", new Message { Role = "user", Content = "child recent", Timestamp = now.AddSeconds(1) }, source: "desktop", parentSessionId: "visible-root");
        index.SaveMessage("hidden-tool", new Message { Role = "user", Content = "hidden recent", Timestamp = now.AddSeconds(2) }, source: "tool");
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance, index);

        var recent = await recall.ListRecentSessionsAsync(limit: 10, ct: CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "visible-root" }, recent.Select(s => s.SessionId).ToArray(),
            "Recent browsing should mirror Python: hide tool-source sessions and omit child/delegation sessions.");
    }

    [TestMethod]
    public async Task SessionSearchTool_DeserializationToleratesMalformedLimitLikePython()
    {
        var transcripts = await CreateTranscriptsWithPriorMemoryAsync("limit-session", "Malformed limit still finds bronze-river.");
        var recall = new TranscriptRecallService(transcripts, NullLogger<TranscriptRecallService>.Instance);
        var tool = new Hermes.Agent.Tools.SessionSearchTool(recall);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        foreach (var json in new[]
        {
            """{"query":"bronze-river","limit":"2"}""",
            """{"query":"bronze-river","limit":"int"}""",
            """{"query":"bronze-river","limit":{"type":"int"}}""",
            """{"query":"bronze-river","limit":null}"""
        })
        {
            var parameters = (Hermes.Agent.Tools.SessionSearchParameters)JsonSerializer.Deserialize(
                json,
                tool.ParametersType,
                jsonOptions)!;

            var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

            Assert.IsTrue(result.Success, result.Content);
            StringAssert.Contains(result.Content, "limit-session");
        }
    }

    [TestMethod]
    public void SystemPrompts_Build_IncludesPythonMemoryAndSessionSearchGuidanceWhenEnabled()
    {
        var prompt = SystemPrompts.Build(includeMemoryGuidance: true, includeSessionSearchGuidance: true);

        StringAssert.Contains(prompt, "You have persistent memory across sessions");
        StringAssert.Contains(prompt, "Do NOT save task progress");
        StringAssert.Contains(prompt, "Write memories as declarative facts");
        StringAssert.Contains(prompt, "use session_search to recall it before asking them to repeat themselves");
        StringAssert.Contains(prompt, "Never answer current time, date, timezone");
        StringAssert.Contains(prompt, "available live-environment integration");
        StringAssert.Contains(prompt, "Do not use interactive `date` prompts");
    }

    [TestMethod]
    public void SystemPrompts_Build_OmitsPythonMemoryAndSessionSearchGuidanceWhenDisabled()
    {
        var prompt = SystemPrompts.Build(includeMemoryGuidance: false, includeSessionSearchGuidance: false);

        Assert.IsFalse(prompt.Contains("You have persistent memory across sessions", StringComparison.Ordinal));
        Assert.IsFalse(prompt.Contains("use session_search to recall it before asking them to repeat themselves", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SessionSearchTool_UsesTranscriptRecallServiceCorpus()
    {
        var transcripts = await CreateTranscriptsWithPriorMemoryAsync("manual-search", "Manual recall should find silver-bridge.");
        var recall = new TranscriptRecallService(transcripts, NullLogger<TranscriptRecallService>.Instance);
        var tool = new Hermes.Agent.Tools.SessionSearchTool(recall);

        var result = await tool.ExecuteAsync(new Hermes.Agent.Tools.SessionSearchParameters
        {
            Query = "silver-bridge",
            Limit = 5
        }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        StringAssert.Contains(result.Content, "manual-search");
        StringAssert.Contains(result.Content, "silver-bridge");
    }

    [TestMethod]
    public void SessionSearchTool_Description_UsesPythonProactiveRecallGuidance()
    {
        var tool = new Hermes.Agent.Tools.SessionSearchTool(new TranscriptRecallService(new TranscriptStore(_tempDir), NullLogger<TranscriptRecallService>.Instance));

        StringAssert.Contains(tool.Description, "Search your long-term memory of past conversations");
        StringAssert.Contains(tool.Description, "TWO MODES");
        StringAssert.Contains(tool.Description, "USE THIS PROACTIVELY");
        StringAssert.Contains(tool.Description, "Start here when the user asks what were we working on");
        StringAssert.Contains(tool.Description, "Use OR between keywords");
    }

    [TestMethod]
    public async Task HermesMemoryOrchestrator_OnTurnStartThenPrefetchAll_CallsProvidersInOrderAndAggregates()
    {
        var calls = new List<string>();
        var first = new RecordingMemoryProvider("first", calls) { PrefetchResult = "first context" };
        var second = new RecordingMemoryProvider("second", calls) { PrefetchResult = "second context" };
        var orchestrator = new HermesMemoryOrchestrator(
            new IMemoryProvider[] { first, second },
            NullLogger<HermesMemoryOrchestrator>.Instance);

        await orchestrator.OnTurnStartAsync(7, "recall basalt", "session-a", CancellationToken.None);
        var context = await orchestrator.PrefetchAllAsync("recall basalt", "session-a", CancellationToken.None);

        CollectionAssert.AreEqual(new[]
        {
            "first:on_turn_start:7:session-a:recall basalt",
            "second:on_turn_start:7:session-a:recall basalt",
            "first:prefetch:session-a:recall basalt",
            "second:prefetch:session-a:recall basalt"
        }, calls);
        StringAssert.Contains(context, "first context");
        StringAssert.Contains(context, "second context");
    }

    [TestMethod]
    public async Task HermesMemoryOrchestrator_ProviderFailuresAreNonFatal()
    {
        var calls = new List<string>();
        var failing = new RecordingMemoryProvider("failing", calls)
        {
            ThrowOnTurnStart = true,
            ThrowOnPrefetch = true,
            ThrowOnSync = true,
            ThrowOnQueue = true
        };
        var healthy = new RecordingMemoryProvider("healthy", calls) { PrefetchResult = "healthy context" };
        var orchestrator = new HermesMemoryOrchestrator(
            new IMemoryProvider[] { failing, healthy },
            NullLogger<HermesMemoryOrchestrator>.Instance);

        await orchestrator.OnTurnStartAsync(1, "query", "session-b", CancellationToken.None);
        var context = await orchestrator.PrefetchAllAsync("query", "session-b", CancellationToken.None);
        await orchestrator.SyncAllAsync("user", "assistant", "session-b", CancellationToken.None);
        await orchestrator.QueuePrefetchAllAsync("user", "session-b", CancellationToken.None);

        StringAssert.Contains(context, "healthy context");
        Assert.IsTrue(calls.Contains("healthy:on_turn_start:1:session-b:query"));
        Assert.IsTrue(calls.Contains("healthy:prefetch:session-b:query"));
        Assert.IsTrue(calls.Contains("healthy:sync:session-b:user=>assistant"));
        Assert.IsTrue(calls.Contains("healthy:queue_prefetch:session-b:user"));
    }

    [TestMethod]
    public async Task HermesMemoryOrchestrator_PreCompressAll_CallsParticipantsInOrder()
    {
        var calls = new List<string>();
        var first = new RecordingCompressionParticipant("first", calls);
        var second = new RecordingCompressionParticipant("second", calls);
        var orchestrator = new HermesMemoryOrchestrator(
            Array.Empty<IMemoryProvider>(),
            NullLogger<HermesMemoryOrchestrator>.Instance,
            new IMemoryCompressionParticipant[] { first, second });
        var evicted = new List<Message>
        {
            new() { Role = "user", Content = "old memory-bearing turn" }
        };

        await orchestrator.PreCompressAllAsync(evicted, "session-compress", CancellationToken.None);

        CollectionAssert.AreEqual(new[]
        {
            "first:pre_compress:session-compress:1:old memory-bearing turn",
            "second:pre_compress:session-compress:1:old memory-bearing turn"
        }, calls);
    }

    [TestMethod]
    public async Task HermesMemoryOrchestrator_PreCompressAll_ParticipantFailuresAreNonFatal()
    {
        var calls = new List<string>();
        var failing = new RecordingCompressionParticipant("failing", calls) { ThrowOnPreCompress = true };
        var healthy = new RecordingCompressionParticipant("healthy", calls);
        var orchestrator = new HermesMemoryOrchestrator(
            Array.Empty<IMemoryProvider>(),
            NullLogger<HermesMemoryOrchestrator>.Instance,
            new IMemoryCompressionParticipant[] { failing, healthy });

        await orchestrator.PreCompressAllAsync(
            new[] { new Message { Role = "assistant", Content = "compress me" } },
            "session-safe",
            CancellationToken.None);

        Assert.IsTrue(calls.Contains("failing:pre_compress:session-safe:1:compress me"));
        Assert.IsTrue(calls.Contains("healthy:pre_compress:session-safe:1:compress me"));
    }

    [TestMethod]
    public async Task CuratedMemoryLifecycleProvider_Prefetch_IsInert()
    {
        var client = new RecordingChatClient();
        var memoryManager = CreateMemoryManager(client);
        var memoryTool = new MemoryTool(memoryManager);
        await memoryTool.ExecuteAsync(new MemoryToolParameters
        {
            Action = "add",
            Target = "memory",
            Content = "Curated marker must not enter dynamic recall amber-codex."
        }, CancellationToken.None);
        var provider = new CuratedMemoryLifecycleProvider(memoryManager);

        var result = await provider.PrefetchAsync("amber-codex", "session-curated", CancellationToken.None);

        Assert.IsTrue(string.IsNullOrWhiteSpace(result),
            "Curated memory participates in lifecycle/handoff only; it must not inject MEMORY.md through PrefetchAsync.");
    }

    [TestMethod]
    public async Task TurnMemoryCoordinator_DoesNotInjectCuratedMemoryThroughDynamicRecall()
    {
        var transcripts = await CreateTranscriptsWithPriorMemoryAsync(
            "prior-dynamic",
            "Prior transcript recall marker river-quartz.");
        var client = new RecordingChatClient { CompleteResponse = "ok" };
        var memoryManager = CreateMemoryManager(client);
        var memoryTool = new MemoryTool(memoryManager);
        await memoryTool.ExecuteAsync(new MemoryToolParameters
        {
            Action = "add",
            Target = "memory",
            Content = "Curated snapshot marker amber-codex."
        }, CancellationToken.None);
        var contextManager = new ContextManager(
            transcripts,
            client,
            new TokenBudget(maxTokens: 8000, recentTurnWindow: 6),
            new PromptBuilder("stable system"),
            NullLogger<ContextManager>.Instance);
        var orchestrator = new HermesMemoryOrchestrator(
            new IMemoryProvider[]
            {
                new CuratedMemoryLifecycleProvider(memoryManager),
                new TranscriptMemoryProvider(new TranscriptRecallService(transcripts, NullLogger<TranscriptRecallService>.Instance))
            },
            NullLogger<HermesMemoryOrchestrator>.Instance);
        var coordinator = new TurnMemoryCoordinator(
            contextManager,
            orchestrator,
            NullLogger<TurnMemoryCoordinator>.Instance);

        var prepared = await coordinator.PrepareFirstCallAsync(
            "current-curated-dedupe",
            "What prior marker should I remember?",
            baseMessages: null,
            TurnMemoryMode.Complete,
            CancellationToken.None);

        var lastUser = prepared.Messages.Last(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(lastUser.Content, "river-quartz");
        Assert.IsFalse(lastUser.Content.Contains("amber-codex", StringComparison.Ordinal),
            "Curated memory must not be duplicated through the dynamic <memory-context> provider lane.");
    }

    [TestMethod]
    public async Task ChatAsync_CompletedTurnSyncsMemoryAndQueuesNextPrefetch()
    {
        var transcripts = new TranscriptStore(_tempDir);
        var client = new RecordingChatClient { CompleteResponse = "completed answer" };
        var calls = new List<string>();
        var provider = new RecordingMemoryProvider("provider", calls);
        var coordinator = CreateCoordinator(client, transcripts, provider);
        var agent = CreateAgent(client, transcripts, coordinator);

        await agent.ChatAsync("sync this turn", new Session { Id = "sync-session" }, CancellationToken.None);

        Assert.IsTrue(calls.Contains("provider:sync:sync-session:sync this turn=>completed answer"));
        Assert.IsTrue(calls.Contains("provider:queue_prefetch:sync-session:sync this turn"));
        Assert.IsTrue(
            calls.IndexOf("provider:sync:sync-session:sync this turn=>completed answer") <
            calls.IndexOf("provider:queue_prefetch:sync-session:sync this turn"),
            "Python reference syncs the completed turn before queueing the next prefetch.");
    }

    [TestMethod]
    public async Task PrepareContextAsync_AutonomySessionCompactsSessionStateBeforePromptBuild()
    {
        var transcripts = new TranscriptStore(_tempDir);
        var client = new RecordingChatClient { CompleteResponse = "summary" };
        var contextManager = new ContextManager(
            transcripts,
            client,
            new TokenBudget(maxTokens: 8000, recentTurnWindow: 6),
            new PromptBuilder("stable system"),
            NullLogger<ContextManager>.Instance);
        var sessionId = "sdv_save-1_haley_default";

        for (var i = 0; i < 18; i++)
            await contextManager.RecordDecisionAsync(sessionId, $"decision-{i}", $"reason-{i}", CancellationToken.None);

        var session = contextManager.GetOrCreateState(sessionId);
        session.ActiveGoal = "keep the current game goal";
        session.OpenQuestions.AddRange(["q-1", "q-2", "q-3", "q-4"]);
        session.ImportantEntities.AddRange(["entity-1", "entity-2", "entity-3", "entity-4", "entity-5", "entity-6"]);

        var messages = await contextManager.PrepareContextAsync(
            sessionId,
            toolSessionId: null,
            userMessage: "continue autonomy",
            retrievedContext: null,
            CancellationToken.None);

        var sessionStateMessage = messages.Single(message =>
            message.Role == "system" &&
            message.Content.StartsWith("[Session State]", StringComparison.Ordinal));
        StringAssert.Contains(sessionStateMessage.Content, "decision-17");
        Assert.IsFalse(sessionStateMessage.Content.Contains("decision-0", StringComparison.Ordinal));
        Assert.IsTrue(sessionStateMessage.Content.Contains("\"openQuestions\"", StringComparison.Ordinal));
        Assert.IsTrue(sessionStateMessage.Content.Contains("\"importantEntities\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ChatAsync_EmptyAssistantResponseDoesNotSyncMemory()
    {
        var transcripts = new TranscriptStore(_tempDir);
        var client = new RecordingChatClient { CompleteResponse = "" };
        var calls = new List<string>();
        var provider = new RecordingMemoryProvider("provider", calls);
        var coordinator = CreateCoordinator(client, transcripts, provider);
        var agent = CreateAgent(client, transcripts, coordinator);

        await agent.ChatAsync("do not sync empty output", new Session { Id = "empty-response" }, CancellationToken.None);

        Assert.IsFalse(calls.Any(c => c.Contains(":sync:", StringComparison.Ordinal)));
        Assert.IsFalse(calls.Any(c => c.Contains(":queue_prefetch:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task SessionSearchTool_EmptyQueryListsRecentSessions()
    {
        var store = new TranscriptStore(_tempDir);
        await store.SaveMessageAsync("recent-a", new Message
        {
            Role = "user",
            Content = "worked on alpine scheduler",
            Timestamp = DateTime.UtcNow.AddMinutes(-10)
        }, CancellationToken.None);
        await store.SaveMessageAsync("recent-b", new Message
        {
            Role = "assistant",
            Content = "finished harbor gateway",
            Timestamp = DateTime.UtcNow.AddMinutes(-5)
        }, CancellationToken.None);
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance);
        var tool = new Hermes.Agent.Tools.SessionSearchTool(recall);

        var result = await tool.ExecuteAsync(new Hermes.Agent.Tools.SessionSearchParameters
        {
            Limit = 5
        }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        var data = JsonDocument.Parse(result.Content).RootElement;
        Assert.IsTrue(data.GetProperty("success").GetBoolean());
        Assert.AreEqual("recent", data.GetProperty("mode").GetString());
        Assert.AreEqual(2, data.GetProperty("count").GetInt32());
        StringAssert.Contains(data.GetProperty("message").GetString(), "Use a keyword query");
        var results = data.GetProperty("results").EnumerateArray().ToArray();
        Assert.AreEqual(2, results.Length);
        var recentA = results.Single(r => r.GetProperty("session_id").GetString() == "recent-a");
        Assert.AreEqual("desktop", recentA.GetProperty("source").GetString());
        Assert.IsTrue(recentA.GetProperty("message_count").GetInt32() > 0);
        StringAssert.Contains(recentA.GetProperty("preview").GetString(), "worked on alpine scheduler");
    }

    [TestMethod]
    public async Task SessionSearchTool_OmittedLimitDefaultsToPythonThree()
    {
        var store = new TranscriptStore(_tempDir);
        for (var i = 0; i < 4; i++)
        {
            await store.SaveMessageAsync($"default-limit-{i}", new Message
            {
                Role = "user",
                Content = $"default limit session {i}",
                Timestamp = DateTime.UtcNow.AddMinutes(i)
            }, CancellationToken.None);
        }

        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance);
        var tool = new Hermes.Agent.Tools.SessionSearchTool(recall);

        var result = await tool.ExecuteAsync(new Hermes.Agent.Tools.SessionSearchParameters(), CancellationToken.None);

        Assert.IsTrue(result.Success);
        var data = JsonDocument.Parse(result.Content).RootElement;
        Assert.AreEqual("recent", data.GetProperty("mode").GetString());
        Assert.AreEqual(3, data.GetProperty("count").GetInt32());
        Assert.AreEqual(3, data.GetProperty("results").GetArrayLength());
        Assert.IsFalse(result.Content.Contains("default-limit-0", StringComparison.OrdinalIgnoreCase),
            "Python session_search defaults omitted limit to 3 and should omit the oldest fourth session.");
    }

    [TestMethod]
    public async Task SessionSearchTool_QueryReturnsSessionLevelSummaryFallback()
    {
        var store = new TranscriptStore(_tempDir);
        await store.SaveMessageAsync("summary-session", new Message { Role = "user", Content = "Please investigate vector-jasmine failures." }, CancellationToken.None);
        await store.SaveMessageAsync("summary-session", new Message { Role = "assistant", Content = "We fixed vector-jasmine by changing retry handling." }, CancellationToken.None);
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance);
        var tool = new Hermes.Agent.Tools.SessionSearchTool(recall);

        var result = await tool.ExecuteAsync(new Hermes.Agent.Tools.SessionSearchParameters
        {
            Query = "vector-jasmine",
            Limit = 3
        }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        var data = JsonDocument.Parse(result.Content).RootElement;
        Assert.IsTrue(data.GetProperty("success").GetBoolean());
        Assert.AreEqual("vector-jasmine", data.GetProperty("query").GetString());
        Assert.AreEqual(1, data.GetProperty("count").GetInt32());
        var session = data.GetProperty("results").EnumerateArray().Single();
        Assert.AreEqual("summary-session", session.GetProperty("session_id").GetString());
        StringAssert.Contains(session.GetProperty("summary").GetString(), "vector-jasmine");
        Assert.AreEqual("desktop", session.GetProperty("source").GetString());
    }

    [TestMethod]
    public async Task SessionSearchTool_CurrentSessionIdExcludesCurrentSessionFromRecentAndSearch()
    {
        var store = new TranscriptStore(_tempDir);
        await store.SaveMessageAsync("current-session", new Message { Role = "user", Content = "current hidden orbit-key" }, CancellationToken.None);
        await store.SaveMessageAsync("prior-session", new Message { Role = "user", Content = "prior visible orbit-key" }, CancellationToken.None);
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance);
        var tool = new Hermes.Agent.Tools.SessionSearchTool(recall);

        var recent = await tool.ExecuteAsync(new Hermes.Agent.Tools.SessionSearchParameters
        {
            CurrentSessionId = "current-session",
            Limit = 5
        }, CancellationToken.None);
        var searched = await tool.ExecuteAsync(new Hermes.Agent.Tools.SessionSearchParameters
        {
            Query = "orbit-key",
            CurrentSessionId = "current-session",
            Limit = 5
        }, CancellationToken.None);

        Assert.IsTrue(recent.Success);
        Assert.IsFalse(recent.Content.Contains("current-session", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(recent.Content, "prior-session");
        Assert.IsTrue(searched.Success);
        Assert.IsFalse(searched.Content.Contains("current hidden orbit-key", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(searched.Content, "prior visible orbit-key");
    }

    [TestMethod]
    public async Task Agent_InjectsCurrentSessionIdIntoSessionAwareToolParameters()
    {
        var store = new TranscriptStore(_tempDir);
        await store.SaveMessageAsync("active-agent-session", new Message { Role = "user", Content = "current-only raven-mint should be excluded" }, CancellationToken.None);
        await store.SaveMessageAsync("prior-agent-session", new Message { Role = "user", Content = "prior raven-mint should be visible" }, CancellationToken.None);
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance);
        var client = new RecordingChatClient();
        client.ToolResponses.Enqueue(new ChatResponse
        {
            FinishReason = "tool_calls",
            ToolCalls = new List<ToolCall>
            {
                new()
                {
                    Id = "call-session-search",
                    Name = "session_search",
                    Arguments = "{\"query\":\"raven-mint\",\"limit\":3}"
                }
            }
        });
        client.ToolResponses.Enqueue(new ChatResponse { Content = "final", FinishReason = "stop" });
        var agent = CreateAgent(client, store);
        agent.RegisterTool(new Hermes.Agent.Tools.SessionSearchTool(recall));
        var session = new Session { Id = "active-agent-session" };

        await agent.ChatAsync("search memory for raven-mint", session, CancellationToken.None);

        var toolMessage = session.Messages.Single(m => string.Equals(m.Role, "tool", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(toolMessage.Content.Contains("current-only raven-mint", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(toolMessage.Content, "prior raven-mint should be visible");
    }

    [TestMethod]
    public void SessionSearchTool_SchemaMatchesPythonNamesAndHidesInjectedCurrentSessionId()
    {
        var store = new TranscriptStore(_tempDir);
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance);
        var agent = new Agent(new RecordingChatClient(), NullLogger<Agent>.Instance);
        agent.RegisterTool(new Hermes.Agent.Tools.SessionSearchTool(recall));

        var schema = agent.GetToolDefinitions().Single(t => t.Name == "session_search").Parameters.GetRawText();

        StringAssert.Contains(schema, "\"query\"");
        StringAssert.Contains(schema, "\"role_filter\"");
        StringAssert.Contains(schema, "\"limit\"");
        Assert.IsFalse(schema.Contains("roleFilter", StringComparison.Ordinal));
        Assert.IsFalse(schema.Contains("maxResults", StringComparison.Ordinal));
        Assert.IsFalse(schema.Contains("currentSessionId", StringComparison.Ordinal));
        Assert.IsFalse(schema.Contains("current_session_id", StringComparison.Ordinal));
        StringAssert.Contains(schema, "user,assistant");
        StringAssert.Contains(schema, "Tool and system messages are not searched");
        Assert.IsFalse(schema.Contains("\"required\"", StringComparison.Ordinal),
            "Python session_search has no required parameters so the model can call it with no arguments for recent-session mode.");
    }

    [TestMethod]
    public async Task TranscriptRecallService_SearchSessionSummariesAsync_SummarizesSessionsConcurrently()
    {
        var store = new TranscriptStore(_tempDir);
        for (var i = 0; i < 3; i++)
        {
            await store.SaveMessageAsync($"parallel-{i}", new Message
            {
                Role = "user",
                Content = $"parallel-token request {i}"
            }, CancellationToken.None);
            await store.SaveMessageAsync($"parallel-{i}", new Message
            {
                Role = "assistant",
                Content = $"parallel-token answer {i}"
            }, CancellationToken.None);
        }

        var summaryClient = new ConcurrentSummaryChatClient();
        var recall = new TranscriptRecallService(
            store,
            NullLogger<TranscriptRecallService>.Instance,
            summaryClient: summaryClient);

        var summaries = await recall.SearchSessionSummariesAsync(
            "parallel-token",
            maxSessions: 3,
            ct: CancellationToken.None);

        Assert.AreEqual(3, summaries.Count);
        Assert.AreEqual(3, summaryClient.CompleteCalls);
        Assert.IsTrue(summaryClient.MaxConcurrent > 1,
            "Python session_search summarizes matching sessions with bounded concurrency, not sequential one-by-one calls.");
        Assert.IsTrue(summaries.All(s => s.Summary.Contains("LLM summary", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task TranscriptRecallService_StripsInjectedMemoryContextBlocksBeforeRecall()
    {
        var store = new TranscriptStore(_tempDir);
        await store.SaveMessageAsync("dirty-session", new Message
        {
            Role = "assistant",
            Content = "Keep durable marker willow-lake. <memory_context>stale nested token should not return</memory_context>"
        }, CancellationToken.None);
        await store.SaveMessageAsync("dirty-session-2", new Message
        {
            Role = "user",
            Content = "Another durable marker cedar-river. <memory-context>old injected block</memory-context>"
        }, CancellationToken.None);
        var recall = new TranscriptRecallService(store, NullLogger<TranscriptRecallService>.Instance);

        var result = await recall.RecallAsync("willow-lake", "current", ct: CancellationToken.None);
        var staleOnly = await recall.SearchAsync("stale nested token", "current", ct: CancellationToken.None);

        Assert.IsTrue(result.Injected);
        StringAssert.Contains(result.ContextBlock!, "willow-lake");
        Assert.IsFalse(result.ContextBlock!.Contains("stale nested token", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(result.ContextBlock!.Contains("<memory_context>", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(0, staleOnly.Count, "Queries must not match stale context from prior injected memory blocks.");
    }

    [TestMethod]
    public async Task ContextManager_PrepareContext_CallsPluginPreCompressBeforeSummarizingEvictedMessages()
    {
        var transcripts = new TranscriptStore(_tempDir);
        await transcripts.SaveMessageAsync("compress-session", new Message { Role = "user", Content = "old turn to summarize" }, CancellationToken.None);
        await transcripts.SaveMessageAsync("compress-session", new Message { Role = "assistant", Content = "old response to summarize" }, CancellationToken.None);
        await transcripts.SaveMessageAsync("compress-session", new Message { Role = "user", Content = "recent turn stays" }, CancellationToken.None);

        var plugin = new RecordingPreCompressPlugin();
        var pluginManager = new PluginManager(NullLogger<PluginManager>.Instance);
        pluginManager.Register(plugin);
        var client = new RecordingChatClient { CompleteResponse = "compressed summary" };
        var contextManager = new ContextManager(
            transcripts,
            client,
            new TokenBudget(maxTokens: 8000, recentTurnWindow: 1),
            new PromptBuilder("stable system"),
            NullLogger<ContextManager>.Instance,
            pluginManager: pluginManager);

        await contextManager.PrepareContextAsync(
            "compress-session",
            "current question",
            retrievedContext: null,
            CancellationToken.None);

        Assert.AreEqual(1, plugin.PreCompressCalls);
        Assert.AreEqual(1, client.CompleteCalls.Count,
            "The fixture must trigger summarization so the pre-compress hook is tied to a real compression path.");
    }

    [TestMethod]
    public async Task ContextManager_PreCompressesMemoryBeforePluginsAndSummarizingEvictedMessages()
    {
        var calls = new List<string>();
        var transcripts = new TranscriptStore(_tempDir);
        await transcripts.SaveMessageAsync("compress-memory-session", new Message { Role = "user", Content = "old memory handoff turn" }, CancellationToken.None);
        await transcripts.SaveMessageAsync("compress-memory-session", new Message { Role = "assistant", Content = "old memory handoff response" }, CancellationToken.None);
        await transcripts.SaveMessageAsync("compress-memory-session", new Message { Role = "user", Content = "recent turn stays" }, CancellationToken.None);

        var pluginManager = new PluginManager(NullLogger<PluginManager>.Instance);
        pluginManager.Register(new RecordingPreCompressPlugin(calls));
        var orchestrator = new HermesMemoryOrchestrator(
            Array.Empty<IMemoryProvider>(),
            NullLogger<HermesMemoryOrchestrator>.Instance,
            new IMemoryCompressionParticipant[] { new RecordingCompressionParticipant("memory", calls) });
        var client = new SequencedSummaryChatClient(calls);
        var contextManager = new ContextManager(
            transcripts,
            client,
            new TokenBudget(maxTokens: 8000, recentTurnWindow: 1),
            new PromptBuilder("stable system"),
            NullLogger<ContextManager>.Instance,
            pluginManager: pluginManager,
            memoryOrchestrator: orchestrator);

        await contextManager.PrepareContextAsync(
            "compress-memory-session",
            "current question",
            retrievedContext: null,
            CancellationToken.None);

        CollectionAssert.AreEqual(new[]
        {
            "memory:pre_compress:compress-memory-session:2:old memory handoff turn",
            "plugin:pre_compress:2",
            "chat:summary"
        }, calls);
    }

    [TestMethod]
    public void DesktopStartup_DoesNotRegisterAutoDreamServiceByDefault()
    {
        var appSourcePath = FindSourceFile("HermesDesktop", "App.xaml.cs");
        var appSource = File.ReadAllText(appSourcePath);

        StringAssert.Contains(appSource, "StartDreamerBackground");
        StringAssert.Contains(appSource, "DreamerService");
        Assert.IsFalse(appSource.Contains("AutoDreamService", StringComparison.Ordinal),
            "AutoDreamService must remain dormant unless a separate scoped task explicitly enables it.");
    }

    [TestMethod]
    public async Task Agent_OptimizedContextIncludesBuiltinMemoryPluginSnapshot()
    {
        var client = new RecordingChatClient();
        var transcripts = new TranscriptStore(_tempDir);
        var memoryManager = CreateMemoryManager(client);
        var memoryTool = new MemoryTool(memoryManager);
        await memoryTool.ExecuteAsync(new MemoryToolParameters
        {
            Action = "add",
            Target = "memory",
            Content = "Optimized context must include marker basalt-memory."
        }, CancellationToken.None);

        var agent = CreateAgentWithBuiltinMemory(client, transcripts, memoryManager, recentTurnWindow: 6);

        await agent.ChatAsync("Use optimized context.", new Session { Id = "plugin-context-session" }, CancellationToken.None);

        var outbound = AssertSingleCompleteCall(client);
        Assert.IsTrue(outbound.Any(m =>
            string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase) &&
            m.Content.Contains("basalt-memory", StringComparison.Ordinal)),
            "The ContextManager/PromptBuilder path must preserve builtin memory plugin system prompt blocks.");
    }

    [TestMethod]
    public async Task Agent_CompressionTurnUsesRefreshedBuiltinMemorySnapshot()
    {
        var client = new RecordingChatClient();
        var transcripts = new TranscriptStore(_tempDir);
        var memoryManager = CreateMemoryManager(client);
        var memoryTool = new MemoryTool(memoryManager);
        await memoryTool.ExecuteAsync(new MemoryToolParameters
        {
            Action = "add",
            Target = "memory",
            Content = "Initial memory before session."
        }, CancellationToken.None);

        var agent = CreateAgentWithBuiltinMemory(client, transcripts, memoryManager, recentTurnWindow: 1);
        var session = new Session { Id = "compression-refresh-session" };
        await agent.ChatAsync("first question", session, CancellationToken.None);

        await memoryTool.ExecuteAsync(new MemoryToolParameters
        {
            Action = "add",
            Target = "memory",
            Content = "Compression refresh must include marker cedar-compress."
        }, CancellationToken.None);

        await agent.ChatAsync("second question triggers compression", session, CancellationToken.None);

        var secondOutbound = client.CompleteCalls.Last(call =>
            call.Any(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                          m.Content.Contains("second question triggers compression", StringComparison.Ordinal)));
        Assert.IsTrue(secondOutbound.Any(m =>
            string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase) &&
            m.Content.Contains("cedar-compress", StringComparison.Ordinal)),
            "The turn that summarizes evicted context must use the refreshed builtin memory snapshot immediately.");
    }

    [TestMethod]
    public async Task StreamChatAsync_CompressionToolContinuationUsesRefreshedBuiltinMemorySnapshot()
    {
        var client = new RecordingChatClient();
        var transcripts = new TranscriptStore(_tempDir);
        var memoryManager = CreateMemoryManager(client);
        var memoryTool = new MemoryTool(memoryManager);
        await memoryTool.ExecuteAsync(new MemoryToolParameters
        {
            Action = "add",
            Target = "memory",
            Content = "Initial streaming memory before session."
        }, CancellationToken.None);

        var agent = CreateAgentWithBuiltinMemory(client, transcripts, memoryManager, recentTurnWindow: 1);
        agent.RegisterTool(new NoopTool());
        var session = new Session { Id = "stream-compression-refresh-session" };
        client.ToolResponses.Enqueue(new ChatResponse { Content = "first stream done", FinishReason = "stop" });
        await foreach (var _ in agent.StreamChatAsync("first stream question", session, CancellationToken.None))
        {
        }

        await memoryTool.ExecuteAsync(new MemoryToolParameters
        {
            Action = "add",
            Target = "memory",
            Content = "Streaming compression refresh must include marker spruce-stream."
        }, CancellationToken.None);
        client.ToolResponses.Enqueue(new ChatResponse
        {
            FinishReason = "tool_calls",
            ToolCalls = new List<ToolCall>
            {
                new() { Id = "stream-call", Name = "noop_tool", Arguments = "{}" }
            }
        });
        client.ToolResponses.Enqueue(new ChatResponse { Content = "second stream done", FinishReason = "stop" });

        await foreach (var _ in agent.StreamChatAsync("second stream question triggers compression", session, CancellationToken.None))
        {
        }

        var continuation = client.CompleteWithToolsCalls.Last();
        Assert.IsTrue(continuation.Any(m =>
            string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase) &&
            m.Content.Contains("spruce-stream", StringComparison.Ordinal)),
            "Streaming tool-loop continuations must use the refreshed builtin memory snapshot after compression.");
    }

    [TestMethod]
    public async Task Agent_CompressionToolContinuationRemovesStaleBuiltinMemoryWhenSnapshotBecomesEmpty()
    {
        var client = new RecordingChatClient();
        var transcripts = new TranscriptStore(_tempDir);
        var memoryManager = CreateMemoryManager(client);
        var memoryTool = new MemoryTool(memoryManager);
        await memoryTool.ExecuteAsync(new MemoryToolParameters
        {
            Action = "add",
            Target = "memory",
            Content = "Temporary memory marker maple-stale."
        }, CancellationToken.None);

        var agent = CreateAgentWithBuiltinMemory(client, transcripts, memoryManager, recentTurnWindow: 1);
        agent.RegisterTool(new NoopTool());
        var session = new Session { Id = "empty-compression-refresh-session" };
        client.ToolResponses.Enqueue(new ChatResponse { Content = "first done", FinishReason = "stop" });
        await agent.ChatAsync("first question", session, CancellationToken.None);

        await memoryTool.ExecuteAsync(new MemoryToolParameters
        {
            Action = "remove",
            Target = "memory",
            OldText = "maple-stale"
        }, CancellationToken.None);
        client.ToolResponses.Enqueue(new ChatResponse
        {
            FinishReason = "tool_calls",
            ToolCalls = new List<ToolCall>
            {
                new() { Id = "empty-call", Name = "noop_tool", Arguments = "{}" }
            }
        });
        client.ToolResponses.Enqueue(new ChatResponse { Content = "second done", FinishReason = "stop" });

        await agent.ChatAsync("second question triggers empty refresh", session, CancellationToken.None);

        var continuation = client.CompleteWithToolsCalls.Last();
        Assert.IsFalse(continuation.Any(m =>
            string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase) &&
            m.Content.Contains("maple-stale", StringComparison.Ordinal)),
            "If compression refreshes builtin memory to empty, tool-loop continuations must not retain the stale turn-start snapshot.");
    }

    private async Task<TranscriptStore> CreateTranscriptsWithPriorMemoryAsync(string priorSessionId, string priorContent)
    {
        var transcripts = new TranscriptStore(_tempDir);
        await transcripts.SaveMessageAsync(priorSessionId, new Message { Role = "user", Content = priorContent }, CancellationToken.None);
        await transcripts.SaveMessageAsync(priorSessionId, new Message { Role = "assistant", Content = "Acknowledged." }, CancellationToken.None);
        return transcripts;
    }

    private static Agent CreateAgent(
        RecordingChatClient client,
        TranscriptStore transcripts,
        TurnMemoryCoordinator? coordinator = null)
    {
        var contextManager = new ContextManager(
            transcripts,
            client,
            new TokenBudget(maxTokens: 8000, recentTurnWindow: 6),
            new PromptBuilder("stable system"),
            NullLogger<ContextManager>.Instance);
        coordinator ??= new TurnMemoryCoordinator(
            contextManager,
            new TranscriptRecallService(transcripts, NullLogger<TranscriptRecallService>.Instance),
            NullLogger<TurnMemoryCoordinator>.Instance);

        return new Agent(
            client,
            NullLogger<Agent>.Instance,
            transcripts: transcripts,
            contextManager: contextManager,
            turnMemoryCoordinator: coordinator);
    }

    private MemoryManager CreateMemoryManager(IChatClient client)
        => new(
            Path.Combine(_tempDir, "memories"),
            client,
            NullLogger<MemoryManager>.Instance);

    private static Agent CreateAgentWithBuiltinMemory(
        RecordingChatClient client,
        TranscriptStore transcripts,
        MemoryManager memoryManager,
        int recentTurnWindow)
    {
        var pluginManager = new PluginManager(NullLogger<PluginManager>.Instance);
        pluginManager.Register(new BuiltinMemoryPlugin(memoryManager));
        var contextManager = new ContextManager(
            transcripts,
            client,
            new TokenBudget(maxTokens: 8000, recentTurnWindow: recentTurnWindow),
            new PromptBuilder("stable system"),
            NullLogger<ContextManager>.Instance,
            pluginManager: pluginManager);
        var coordinator = new TurnMemoryCoordinator(
            contextManager,
            new TranscriptRecallService(transcripts, NullLogger<TranscriptRecallService>.Instance),
            NullLogger<TurnMemoryCoordinator>.Instance);

        return new Agent(
            client,
            NullLogger<Agent>.Instance,
            transcripts: transcripts,
            contextManager: contextManager,
            pluginManager: pluginManager,
            turnMemoryCoordinator: coordinator);
    }

    private static TurnMemoryCoordinator CreateCoordinator(
        RecordingChatClient client,
        TranscriptStore transcripts,
        params IMemoryProvider[] providers)
    {
        var contextManager = new ContextManager(
            transcripts,
            client,
            new TokenBudget(maxTokens: 8000, recentTurnWindow: 6),
            new PromptBuilder("stable system"),
            NullLogger<ContextManager>.Instance);
        var orchestrator = new HermesMemoryOrchestrator(
            providers,
            NullLogger<HermesMemoryOrchestrator>.Instance);

        return new TurnMemoryCoordinator(
            contextManager,
            orchestrator,
            NullLogger<TurnMemoryCoordinator>.Instance);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static List<Message> AssertSingleCompleteCall(RecordingChatClient client)
    {
        Assert.AreEqual(1, client.CompleteCalls.Count);
        return client.CompleteCalls[0];
    }

    private sealed class NoopTool : ITool
    {
        public string Name => "noop_tool";
        public string Description => "No-op test tool.";
        public Type ParametersType => typeof(NoopParams);
        public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
            => Task.FromResult(ToolResult.Ok("noop"));
    }

    private sealed class NoopParams
    {
    }

    private sealed class ThrowingTranscriptObserver : ITranscriptMessageObserver
    {
        public int Attempts { get; private set; }

        public Task OnMessageSavedAsync(string sessionId, Message message, CancellationToken ct)
        {
            Attempts++;
            throw new InvalidOperationException("index unavailable");
        }
    }

    private sealed class RecordingMemoryProvider : IMemoryProvider
    {
        private readonly List<string> _calls;

        public RecordingMemoryProvider(string name, List<string> calls)
        {
            Name = name;
            _calls = calls;
        }

        public string Name { get; }
        public string PrefetchResult { get; init; } = "";
        public bool ThrowOnTurnStart { get; init; }
        public bool ThrowOnPrefetch { get; init; }
        public bool ThrowOnSync { get; init; }
        public bool ThrowOnQueue { get; init; }

        public Task OnTurnStartAsync(int turnNumber, string userMessage, string sessionId, CancellationToken ct)
        {
            _calls.Add($"{Name}:on_turn_start:{turnNumber}:{sessionId}:{userMessage}");
            if (ThrowOnTurnStart)
                throw new InvalidOperationException($"{Name} turn start failed");
            return Task.CompletedTask;
        }

        public Task<string?> PrefetchAsync(string query, string sessionId, CancellationToken ct)
        {
            _calls.Add($"{Name}:prefetch:{sessionId}:{query}");
            if (ThrowOnPrefetch)
                throw new InvalidOperationException($"{Name} prefetch failed");
            return Task.FromResult<string?>(PrefetchResult);
        }

        public Task SyncTurnAsync(string userContent, string assistantContent, string sessionId, CancellationToken ct)
        {
            _calls.Add($"{Name}:sync:{sessionId}:{userContent}=>{assistantContent}");
            if (ThrowOnSync)
                throw new InvalidOperationException($"{Name} sync failed");
            return Task.CompletedTask;
        }

        public Task QueuePrefetchAsync(string query, string sessionId, CancellationToken ct)
        {
            _calls.Add($"{Name}:queue_prefetch:{sessionId}:{query}");
            if (ThrowOnQueue)
                throw new InvalidOperationException($"{Name} queue failed");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCompressionParticipant : IMemoryCompressionParticipant
    {
        private readonly List<string> _calls;

        public RecordingCompressionParticipant(string name, List<string> calls)
        {
            Name = name;
            _calls = calls;
        }

        public string Name { get; }
        public bool ThrowOnPreCompress { get; init; }

        public Task OnPreCompressAsync(IReadOnlyList<Message> messages, string sessionId, CancellationToken ct)
        {
            _calls.Add($"{Name}:pre_compress:{sessionId}:{messages.Count}:{messages[0].Content}");
            if (ThrowOnPreCompress)
                throw new InvalidOperationException($"{Name} pre-compress failed");
            return Task.CompletedTask;
        }
    }

    private static string FindSourceFile(params string[] relativeParts)
    {
        var relativePath = Path.Combine(relativeParts);
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("Could not find source file from test output directory.", relativePath);
    }

    private sealed class RecordingPreCompressPlugin : PluginBase
    {
        private readonly List<string>? _calls;

        public RecordingPreCompressPlugin(List<string>? calls = null)
        {
            _calls = calls;
        }

        public override string Name => "recording-pre-compress";
        public override string Category => "memory";
        public int PreCompressCalls { get; private set; }

        public override Task OnPreCompressAsync(IReadOnlyList<Message> messages, CancellationToken ct)
        {
            PreCompressCalls++;
            _calls?.Add($"plugin:pre_compress:{messages.Count}");
            return Task.CompletedTask;
        }
    }

    private sealed class SequencedSummaryChatClient : IChatClient
    {
        private readonly List<string> _calls;

        public SequencedSummaryChatClient(List<string> calls)
        {
            _calls = calls;
        }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
        {
            _calls.Add("chat:summary");
            return Task.FromResult("compressed summary");
        }

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<string> StreamAsync(
            IEnumerable<Message> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public List<List<Message>> CompleteCalls { get; } = new();
        public List<List<Message>> CompleteWithToolsCalls { get; } = new();
        public Queue<ChatResponse> ToolResponses { get; } = new();
        public string CompleteResponse { get; init; } = "ok";

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
        {
            CompleteCalls.Add(Clone(messages));
            return Task.FromResult(CompleteResponse);
        }

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            CompleteWithToolsCalls.Add(Clone(messages));
            return Task.FromResult(ToolResponses.Count > 0
                ? ToolResponses.Dequeue()
                : new ChatResponse { Content = "done", FinishReason = "stop" });
        }

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            CompleteCalls.Add(Clone(messages));
            yield return "stream";
            await Task.CompletedTask;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            CompleteCalls.Add(Clone(messages));
            yield return new StreamEvent.TokenDelta("stream");
            yield return new StreamEvent.MessageComplete("stop");
            await Task.CompletedTask;
        }

        private static List<Message> Clone(IEnumerable<Message> messages)
            => messages.Select(m => new Message
            {
                Role = m.Role,
                Content = m.Content,
                Timestamp = m.Timestamp,
                ToolCallId = m.ToolCallId,
                ToolName = m.ToolName,
                ToolCalls = m.ToolCalls
            }).ToList();
    }

    private sealed class ConcurrentSummaryChatClient : IChatClient
    {
        private int _current;
        private int _maxConcurrent;

        public int CompleteCalls { get; private set; }
        public int MaxConcurrent => _maxConcurrent;

        public async Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
        {
            CompleteCalls++;
            var current = Interlocked.Increment(ref _current);
            try
            {
                var observed = _maxConcurrent;
                while (current > observed)
                {
                    var prior = Interlocked.CompareExchange(ref _maxConcurrent, current, observed);
                    if (prior == observed)
                        break;
                    observed = prior;
                }

                await Task.Delay(75, ct);
                return "LLM summary";
            }
            finally
            {
                Interlocked.Decrement(ref _current);
            }
        }

        public Task<ChatResponse> CompleteWithToolsAsync(IEnumerable<Message> messages, IEnumerable<ToolDefinition> tools, CancellationToken ct)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
