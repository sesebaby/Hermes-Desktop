using System.Runtime.CompilerServices;
using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Memory;
using Hermes.Agent.Runtime;
using Hermes.Agent.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public class NpcAutonomyLoopTests
{
    [TestMethod]
    public async Task RunOneTickAsync_GathersObservationAndEventsWithoutCallingCommands()
    {
        var at = new DateTime(2026, 4, 30, 9, 30, 0, DateTimeKind.Utc);
        var descriptor = CreateDescriptor("haley");
        var factStore = new NpcObservationFactStore();
        var commands = new CountingCommandService();
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            at,
            "Haley is near the town fountain.",
            ["location=Town", "tile=42,17"]));
        var events = new FakeEventSource(
            [
                new GameEventRecord("evt-2", "proximity", "haley", at.AddSeconds(1), "The farmer entered Haley's proximity."),
                new GameEventRecord("evt-3", "time_changed", null, at.AddMinutes(10), "The clock advanced."),
                new GameEventRecord("evt-4", "proximity", "penny", at.AddSeconds(2), "The farmer entered Penny's proximity.")
            ]);
        var adapter = new FakeGameAdapter(commands, queries, events);
        var loop = new NpcAutonomyLoop(adapter, factStore);

        var result = await loop.RunOneTickAsync(descriptor, new GameEventCursor("evt-1"), CancellationToken.None);

        Assert.AreEqual("haley", queries.LastNpcId);
        Assert.AreEqual("evt-1", events.LastCursor?.Since);
        Assert.AreEqual(0, commands.SubmitCalls);
        Assert.AreEqual(0, commands.StatusCalls);
        Assert.AreEqual(0, commands.CancelCalls);

        Assert.AreEqual(1, result.ObservationFacts);
        Assert.AreEqual(2, result.EventFacts);
        var facts = factStore.Snapshot(descriptor);
        Assert.AreEqual(3, facts.Count);
        Assert.IsTrue(facts.Any(fact => fact.SourceKind == "observation"));
        Assert.IsTrue(facts.Any(fact => fact.SourceId == "evt-2"));
        Assert.IsTrue(facts.Any(fact => fact.SourceId == "evt-3"));
        Assert.IsFalse(facts.Any(fact => fact.SourceId == "evt-4"));
    }

    [TestMethod]
    public async Task RunOneTickAsync_AdvancesCursorUsingEventSequenceWhenPresent()
    {
        var at = new DateTime(2026, 4, 30, 9, 40, 0, DateTimeKind.Utc);
        var descriptor = CreateDescriptor("haley");
        var factStore = new NpcObservationFactStore();
        var commands = new CountingCommandService();
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            at,
            "Haley is near the town fountain.",
            ["location=Town"]));
        var events = new FakeEventSource(
            new GameEventBatch(
                [
                    new GameEventRecord("evt-11", "proximity", "haley", at.AddSeconds(1), "The farmer entered Haley's proximity.", Sequence: 11),
                    new GameEventRecord("evt-12", "proximity", "penny", at.AddSeconds(2), "The farmer entered Penny's proximity.", Sequence: 12)
                ],
                new GameEventCursor("evt-12", 15)));
        var adapter = new FakeGameAdapter(commands, queries, events);
        var loop = new NpcAutonomyLoop(adapter, factStore);

        var result = await loop.RunOneTickAsync(descriptor, new GameEventCursor("evt-10", 10), CancellationToken.None);

        Assert.AreEqual("evt-12", result.NextEventCursor?.Since);
        Assert.AreEqual(15L, result.NextEventCursor?.Sequence);
    }

    [TestMethod]
    public async Task RunOneTickAsync_ObservesBeforeAgentDecisionAndSubmitsVisibleFallbackSpeak()
    {
        var steps = new List<string>();
        var at = new DateTime(2026, 4, 30, 10, 0, 0, DateTimeKind.Utc);
        var descriptor = CreateDescriptor("haley");
        var factStore = new NpcObservationFactStore();
        var commands = new CountingCommandService();
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            at,
            "Haley is at the fountain.",
            ["location=Town"]));
        queries.OnObserve = () => steps.Add("observe");
        var events = new FakeEventSource(
            [new GameEventRecord("evt-2", "time_changed", null, at.AddMinutes(10), "The clock advanced.")]);
        events.OnPoll = () => steps.Add("poll");
        var agent = new FakeAgent(() => steps.Add("decide"), "我在喷泉边等一下。");
        var adapter = new FakeGameAdapter(commands, queries, events);
        var loop = new NpcAutonomyLoop(adapter, factStore, agent);

        var result = await loop.RunOneTickAsync(descriptor, new GameEventCursor("evt-1"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "observe", "poll", "decide" }, steps);
        Assert.AreEqual(1, agent.ChatCalls);
        Assert.IsTrue(agent.LastMessage?.Contains("location=Town", StringComparison.Ordinal) ?? false);
        Assert.IsTrue(agent.LastMessage?.Contains("evt-2", StringComparison.Ordinal) ?? false);
        Assert.AreEqual("我在喷泉边等一下。", result.DecisionResponse);
        Assert.AreEqual(0, commands.SubmitCalls);
    }

    [TestMethod]
    public async Task RunOneTickAsync_SetsAutonomySessionMarkerOnDecisionSession()
    {
        Session? capturedSession = null;
        var descriptor = CreateDescriptor("haley");
        var loop = new NpcAutonomyLoop(
            new FakeGameAdapter(
                new CountingCommandService(),
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is idle.",
                    ["location=Town"])),
                new FakeEventSource([])),
            new NpcObservationFactStore(),
            new FakeAgent(() => { }, mutateSession: session => capturedSession = session));

        await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

        Assert.IsNotNull(capturedSession);
        Assert.IsTrue(capturedSession.State.TryGetValue(StardewAutonomySessionKeys.IsAutonomyTurn, out var marker));
        Assert.AreEqual(true, marker);
        Assert.AreEqual("haley", capturedSession.State["npcId"]);
    }

    [TestMethod]
    public async Task RunOneTickAsync_DecisionMessageDoesNotReuseHistoricalMoveCandidates()
    {
        var descriptor = CreateDescriptor("haley");
        var factStore = new NpcObservationFactStore();
        var agent = new FakeAgent(() => { });
        var loop = new NpcAutonomyLoop(
            new FakeGameAdapter(
                new CountingCommandService(),
                new FakeQueryService(new GameObservation("haley", "stardew-valley", DateTime.UtcNow, "unused", [])),
                new FakeEventSource([])),
            factStore,
            agent);

        await loop.RunOneTickAsync(
            descriptor,
            new GameObservation(
                "haley",
                "stardew-valley",
                new DateTime(2026, 4, 30, 10, 0, 0, DateTimeKind.Utc),
                "Haley can step east.",
                ["location=HaleyHouse", "moveCandidate[0]=locationName=HaleyHouse,x=9,y=7,reason=same_location_safe_reposition"]),
            new GameEventBatch([], new GameEventCursor()),
            CancellationToken.None);

        await loop.RunOneTickAsync(
            descriptor,
            new GameObservation(
                "haley",
                "stardew-valley",
                new DateTime(2026, 4, 30, 10, 1, 0, DateTimeKind.Utc),
                "Haley can step south.",
                ["location=HaleyHouse", "moveCandidate[0]=locationName=HaleyHouse,x=8,y=8,reason=same_location_safe_reposition"]),
            new GameEventBatch([], new GameEventCursor()),
            CancellationToken.None);

        Assert.AreEqual(2, agent.Messages.Count);
        StringAssert.Contains(agent.Messages[1], "x=8,y=8");
        Assert.IsFalse(
            agent.Messages[1].Contains("x=9,y=7", StringComparison.Ordinal),
            "Old move candidates must remain in persisted debug facts but must not re-enter the current decision prompt.");
        Assert.IsTrue(
            factStore.Snapshot(descriptor).Any(fact => fact.Facts.Any(value => value.Contains("x=9,y=7", StringComparison.Ordinal))),
            "The store can retain historical observations for diagnostics; only the live decision prompt must be current-only.");
    }

    [TestMethod]
    public async Task RunOneTickAsync_DecisionMessageUsesChineseTaskContinuityGuidance()
    {
        var descriptor = CreateDescriptor("haley");
        var agent = new FakeAgent(() => { });
        var loop = new NpcAutonomyLoop(
            new FakeGameAdapter(
                new CountingCommandService(),
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is idle and can continue prior commitments.",
                    ["location=Town", "moveCandidate[0]=locationName=Town,x=42,y=17"])),
                new FakeEventSource([])),
            new NpcObservationFactStore(),
            agent);

        await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

        Assert.IsNotNull(agent.LastMessage);
        StringAssert.Contains(agent.LastMessage, "先看当前观察事实和 active todo");
        StringAssert.Contains(agent.LastMessage, "玩家给过的约定");
        StringAssert.Contains(agent.LastMessage, "blocked 或 failed");
        StringAssert.Contains(agent.LastMessage, "用 stardew_speak");
        StringAssert.Contains(agent.LastMessage, "不要把事件当成玩家的新命令");
        Assert.IsFalse(
            agent.LastMessage.Contains("Use these passive game facts", StringComparison.Ordinal),
            "Autonomy decision prompt should use Chinese plain-language continuity guidance instead of the old generic English instruction.");
    }

    [TestMethod]
    public async Task RunOneTickAsync_DecisionMessageMarksObservationTimestampAsRecordTimeNotGameTime()
    {
        var descriptor = CreateDescriptor("haley");
        var agent = new FakeAgent(() => { });
        var loop = new NpcAutonomyLoop(
            new FakeGameAdapter(
                new CountingCommandService(),
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    new DateTime(2026, 5, 5, 3, 8, 0, DateTimeKind.Utc),
                    "Haley is in town in the afternoon.",
                    ["location=Town", "tile=42,17", "gameTime=1430", "gameClock=14:30"])),
                new FakeEventSource([])),
            new NpcObservationFactStore(),
            agent);

        await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

        Assert.IsNotNull(agent.LastMessage);
        StringAssert.Contains(agent.LastMessage, "记录时间不是星露谷游戏内时间");
        StringAssert.Contains(agent.LastMessage, "gameTime/gameClock 才是游戏内时间");
        StringAssert.Contains(agent.LastMessage, "gameTime=1430");
        StringAssert.Contains(agent.LastMessage, "gameClock=14:30");
    }

    [TestMethod]
    public async Task RunOneTickAsync_WithRuntimeInstance_WritesActivityLogAndUpdatesTrace()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-loop-trace-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var descriptor = CreateDescriptor("haley");
            var ns = new NpcNamespace(tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
            var instance = new NpcRuntimeInstance(descriptor, ns);
            await instance.StartAsync(CancellationToken.None);
            var logPath = Path.Combine(ns.ActivityPath, "runtime.jsonl");
            var factStore = new NpcObservationFactStore();
            var adapter = new FakeGameAdapter(
                new CountingCommandService(),
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is idle.",
                    ["location=Town"])),
                new FakeEventSource([]));
            var loop = new NpcAutonomyLoop(
                adapter,
                factStore,
                logWriter: new NpcRuntimeLogWriter(logPath),
                traceIdFactory: () => "trace-1");

            var result = await loop.RunOneTickAsync(instance, new GameEventCursor(null), CancellationToken.None);

            Assert.AreEqual("trace-1", result.TraceId);
            Assert.AreEqual("trace-1", instance.Snapshot().LastTraceId);
            var line = File.ReadAllLines(logPath).Single();
            using var doc = JsonDocument.Parse(line);
            Assert.AreEqual("trace-1", doc.RootElement.GetProperty("traceId").GetString());
            Assert.AreEqual("haley", doc.RootElement.GetProperty("npcId").GetString());
            Assert.AreEqual("completed", doc.RootElement.GetProperty("stage").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RunOneTickAsync_WhenDecisionNarratesMovementWithoutMoveToolCall_WritesDiagnosticLog()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-loop-diagnostic-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var descriptor = CreateDescriptor("haley");
            var ns = new NpcNamespace(tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
            ns.EnsureDirectories();
            var logPath = Path.Combine(ns.ActivityPath, "runtime.jsonl");
            var factStore = new NpcObservationFactStore();
            var adapter = new FakeGameAdapter(
                new CountingCommandService(),
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is in her room with a safe candidate.",
                    [
                        "location=HaleyHouse",
                        "moveCandidate[0]=locationName=HaleyHouse,x=6,y=4,reason=same_location_safe_reposition"
                    ])),
                new FakeEventSource([]));
            var agent = new FakeAgent(() => { }, "她转身走向床铺。");
            var loop = new NpcAutonomyLoop(
                adapter,
                factStore,
                agent,
                logWriter: new NpcRuntimeLogWriter(logPath),
                traceIdFactory: () => "trace-narrative-move");

            await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

            var records = File.ReadAllLines(logPath)
                .Select(line => JsonDocument.Parse(line))
                .ToArray();
            try
            {
                Assert.IsTrue(records.Any(doc =>
                    doc.RootElement.GetProperty("actionType").GetString() == "diagnostic" &&
                    doc.RootElement.GetProperty("target").GetString() == "stardew_move" &&
                    doc.RootElement.GetProperty("stage").GetString() == "warning" &&
                    doc.RootElement.GetProperty("result").GetString() == "narrative_move_without_stardew_move"),
                    "A movement-looking final reply without a stardew_move tool call should be visible in runtime.jsonl.");
            }
            finally
            {
                foreach (var record in records)
                    record.Dispose();
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RunOneTickAsync_WhenPromisedTaskBlocks_WritesTaskContinuityEvidenceAndRequiresFeedbackAttempt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-loop-continuity-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var descriptor = CreateDescriptor("haley");
            var ns = new NpcNamespace(tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
            ns.EnsureDirectories();
            var instance = new NpcRuntimeInstance(descriptor, ns);
            await instance.StartAsync(CancellationToken.None);
            instance.TodoStore.Write(
                descriptor.SessionId,
                [new SessionTodoInput("1", "Meet player at the beach", "pending")]);
            var logPath = Path.Combine(ns.ActivityPath, "runtime.jsonl");
            var agent = new FakeAgent(
                () => { },
                "I told the player the beach path is blocked.",
                session =>
                {
                    session.AddMessage(new Message
                    {
                        Role = "assistant",
                        Content = "",
                        ToolCalls =
                        [
                            new ToolCall
                            {
                                Id = "call-move",
                                Name = "stardew_move",
                                Arguments = """{"destination":"beach.pier","reason":"continue the beach promise"}"""
                            },
                            new ToolCall
                            {
                                Id = "call-todo",
                                Name = "todo",
                                Arguments = """{"todos":[{"id":"1","content":"Meet player at the beach","status":"blocked","reason":"path_blocked"}]}"""
                            },
                            new ToolCall
                            {
                                Id = "call-speak",
                                Name = "stardew_speak",
                                Arguments = """{"text":"The beach path is blocked right now.","channel":"player"}"""
                            }
                        ]
                    });
                    session.AddMessage(new Message
                    {
                        Role = "tool",
                        ToolCallId = "call-move",
                        ToolName = "stardew_move",
                        Content = """{"accepted":true,"commandId":"cmd-blocked","status":"queued","failureReason":null,"traceId":"trace-continuity","finalStatus":{"commandId":"cmd-blocked","npcId":"haley","action":"move","status":"blocked","progress":1,"blockedReason":"path_blocked","errorCode":"path_blocked"}}"""
                    });
                    session.AddMessage(new Message
                    {
                        Role = "tool",
                        ToolCallId = "call-todo",
                        ToolName = "todo",
                        Content = """{"todos":[{"id":"1","content":"Meet player at the beach","status":"blocked","reason":"path_blocked"}]}"""
                    });
                    session.AddMessage(new Message
                    {
                        Role = "tool",
                        ToolCallId = "call-speak",
                        ToolName = "stardew_speak",
                        Content = """{"accepted":true,"commandId":"cmd-speak","status":"completed","failureReason":null,"traceId":"trace-speak","finalStatus":{"commandId":"cmd-speak","npcId":"haley","action":"speak","status":"completed","progress":1}}"""
                    });
                });
            var loop = new NpcAutonomyLoop(
                new FakeGameAdapter(
                    new CountingCommandService(),
                    new FakeQueryService(new GameObservation(
                        "haley",
                        "stardew-valley",
                        DateTime.UtcNow,
                        "Haley is trying to continue a player promise.",
                        ["location=Town", "destination[0]=destinationId=beach.pier,reason=continue the beach promise"])),
                    new FakeEventSource([])),
                new NpcObservationFactStore(),
                agent,
                logWriter: new NpcRuntimeLogWriter(logPath),
                traceIdFactory: () => "trace-continuity");

            await loop.RunOneTickAsync(instance, new GameEventCursor(null), CancellationToken.None);

            var records = ReadRuntimeLogRecords(logPath);
            AssertRuntimeRecord(records, "observed_active_todo", "observed", "active");
            AssertRuntimeRecord(records, "action_submitted", "submitted", "submitted");
            AssertRuntimeRecord(records, "command_terminal", "terminal", "blocked", "cmd-blocked");
            AssertRuntimeRecord(records, "todo_update_tool_result", "task_written", "blocked");
            AssertRuntimeRecord(records, "feedback_attempted", "feedback", "attempted", "cmd-speak");
            Assert.IsFalse(records.Any(record =>
                record.GetProperty("actionType").GetString() == "task_continuity" &&
                record.GetProperty("target").GetString() == "feedback_missing"),
                "Player-promised blocked tasks must not pass closure with feedback_missing on the main path.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RunOneTickAsync_WhenDecisionHasOnlyNoVisibleSleepText_DoesNotSubmitFallbackSpeak()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-loop-no-visible-fallback-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var descriptor = CreateDescriptor("haley");
            var ns = new NpcNamespace(tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
            ns.EnsureDirectories();
            var logPath = Path.Combine(ns.ActivityPath, "runtime.jsonl");
            var commands = new CountingCommandService();
            var factStore = new NpcObservationFactStore();
            var adapter = new FakeGameAdapter(
                commands,
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is asleep.",
                    ["location=HaleyHouse"])),
                new FakeEventSource([]));
            var agent = new FakeAgent(() => { }, "zzz……💤\n\n*安静地睡着*");
            var loop = new NpcAutonomyLoop(
                adapter,
                factStore,
                agent,
                logWriter: new NpcRuntimeLogWriter(logPath),
                traceIdFactory: () => "trace-no-visible-line");

            await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

            Assert.AreEqual(0, commands.SubmitCalls);
            Assert.IsTrue(File.ReadAllText(logPath).Contains("no_tool_decision:no_visible_line", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RunOneTickAsync_WhenAgentHitsToolIterationLimit_DoesNotWriteFallbackAsTick()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-loop-tool-limit-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var descriptor = CreateDescriptor("penny");
            var ns = new NpcNamespace(tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
            ns.EnsureDirectories();
            var logPath = Path.Combine(ns.ActivityPath, "runtime.jsonl");
            var fallback = "I've reached the maximum number of tool call iterations. Here's what I've accomplished so far based on the conversation above.";
            var loop = new NpcAutonomyLoop(
                new FakeGameAdapter(
                    new CountingCommandService(),
                    new FakeQueryService(new GameObservation(
                        "penny",
                        "stardew-valley",
                        DateTime.UtcNow,
                        "Penny is idle.",
                        ["location=Trailer"])),
                    new FakeEventSource([])),
                new NpcObservationFactStore(),
                new FakeAgent(() => { }, fallback),
                logWriter: new NpcRuntimeLogWriter(logPath),
                traceIdFactory: () => "trace-tool-limit");

            var result = await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

            Assert.IsNull(result.DecisionResponse, "Agent framework fallback text should not become an NPC decision response.");
            var records = File.ReadAllLines(logPath)
                .Select(line => JsonDocument.Parse(line))
                .ToArray();
            try
            {
                var tick = records.Single(doc => doc.RootElement.GetProperty("actionType").GetString() == "tick");
                Assert.AreEqual("observed:1", tick.RootElement.GetProperty("result").GetString());
                Assert.IsFalse(
                    records.Any(doc => doc.RootElement.GetProperty("result").GetString()?.Contains("maximum number of tool call iterations", StringComparison.Ordinal) == true),
                    "Framework max-tool fallback must not be written as user-visible NPC activity text.");
                Assert.IsTrue(records.Any(doc =>
                    doc.RootElement.GetProperty("actionType").GetString() == "diagnostic" &&
                    doc.RootElement.GetProperty("target").GetString() == "tool_budget" &&
                    doc.RootElement.GetProperty("result").GetString() == "max_tool_iterations"),
                    "The dropped framework fallback still needs an explicit diagnostic record.");
            }
            finally
            {
                foreach (var record in records)
                    record.Dispose();
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RunOneTickAsync_WhenDecisionIsStatusOnly_DoesNotSubmitFallbackSpeak()
    {
        var descriptor = CreateDescriptor("haley");
        var commands = new CountingCommandService();
        var loop = new NpcAutonomyLoop(
            new FakeGameAdapter(
                commands,
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is asleep.",
                    ["location=HaleyHouse"])),
                new FakeEventSource([])),
            new NpcObservationFactStore(),
            new FakeAgent(() => { }, "*继续休息——等待天亮再行动。*"));

        await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

        Assert.AreEqual(0, commands.SubmitCalls);
    }

    [DataTestMethod]
    [DataRow("*天亮了再说～*")]
    [DataRow("拖车里很安静。")]
    [DataRow("那些话已经好好收进心底。")]
    [DataRow("同一秒钟，潘妮在拖车里继续睡着。")]
    public async Task RunOneTickAsync_WhenDecisionIsNarrationOnly_DoesNotSubmitFallbackSpeak(string response)
    {
        var descriptor = CreateDescriptor("penny");
        var commands = new CountingCommandService();
        var loop = new NpcAutonomyLoop(
            new FakeGameAdapter(
                commands,
                new FakeQueryService(new GameObservation(
                    "penny",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Penny is asleep.",
                    ["location=Trailer"])),
                new FakeEventSource([])),
            new NpcObservationFactStore(),
            new FakeAgent(() => { }, response));

        await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

        Assert.AreEqual(0, commands.SubmitCalls);
    }

    [TestMethod]
    public async Task RunOneTickAsync_WhenDecisionIsDirectUnquotedSpeech_DoesNotSubmitFallbackSpeak()
    {
        var descriptor = CreateDescriptor("haley");
        var commands = new CountingCommandService();
        var loop = new NpcAutonomyLoop(
            new FakeGameAdapter(
                commands,
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is awake.",
                    ["location=HaleyHouse"])),
                new FakeEventSource([])),
            new NpcObservationFactStore(),
            new FakeAgent(() => { }, "我在喷泉边等一下。"));

        await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

        Assert.AreEqual(0, commands.SubmitCalls);
    }

    [TestMethod]
    public async Task RunOneTickAsync_WhenDecisionContainsQuotedLine_DoesNotSubmitFallbackSpeak()
    {
        var descriptor = CreateDescriptor("haley");
        var commands = new CountingCommandService();
        var loop = new NpcAutonomyLoop(
            new FakeGameAdapter(
                commands,
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is asleep.",
                    ["location=HaleyHouse"])),
                new FakeEventSource([])),
            new NpcObservationFactStore(),
            new FakeAgent(() => { }, "*她翻了个身，含糊地说——*\n\n\"明天再回你……\""));

        await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

        Assert.AreEqual(0, commands.SubmitCalls);
    }

    [TestMethod]
    public async Task RunOneTickAsync_WhenDecisionQuotesHistoricalPhoneMessage_DoesNotSubmitFallbackSpeak()
    {
        var descriptor = CreateDescriptor("penny");
        var commands = new CountingCommandService();
        var loop = new NpcAutonomyLoop(
            new FakeGameAdapter(
                commands,
                new FakeQueryService(new GameObservation(
                    "penny",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Penny is asleep.",
                    ["location=Trailer"])),
                new FakeEventSource([])),
            new NpcObservationFactStore(),
            new FakeAgent(() => { }, "她在心里把手机里的那几条消息又默念了一遍。\n\n——*\"我回去的，等我这边农场忙完。\"*\n\n她轻轻嗯了一声。"));

        await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

        Assert.AreEqual(0, commands.SubmitCalls);
    }

    [TestMethod]
    public async Task RunOneTickAsync_WhenDecisionRecapsSentPhoneMessages_DoesNotSubmitFallbackSpeak()
    {
        var descriptor = CreateDescriptor("penny");
        var commands = new CountingCommandService();
        var loop = new NpcAutonomyLoop(
            new FakeGameAdapter(
                commands,
                new FakeQueryService(new GameObservation(
                    "penny",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Penny is asleep.",
                    ["location=Trailer"])),
                new FakeEventSource([])),
            new NpcObservationFactStore(),
            new FakeAgent(() => { }, "🌙 **凌晨两点半，trailer 里安安静静。**\n\n我刚刚跟他说了\"天亮了再来找我\"，那两条消息应该已经送到他手机上了。\n\n大半夜的不用再催他，让他安心忙完农场再过来吧。\n\n**⏳ 持续等待。暂无新行动。**"));

        await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

        Assert.AreEqual(0, commands.SubmitCalls);
    }

    [TestMethod]
    public async Task RunOneTickAsync_WhenDecisionQuotesCurrentDreamTalk_DoesNotSubmitFallbackSpeak()
    {
        var descriptor = CreateDescriptor("haley");
        var commands = new CountingCommandService();
        var loop = new NpcAutonomyLoop(
            new FakeGameAdapter(
                commands,
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is asleep.",
                    ["location=HaleyHouse"])),
                new FakeEventSource([])),
            new NpcObservationFactStore(),
            new FakeAgent(() => { }, "*被窝里翻了个身，迷迷糊糊地嘟囔了一句……*\n\n\"唔……明天再说啦……\""));

        await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

        Assert.AreEqual(0, commands.SubmitCalls);
    }

    [TestMethod]
    public async Task RunOneTickAsync_WithDecisionResponse_WritesNpcLocalMemory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-loop-memory-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var descriptor = CreateDescriptor("haley");
            var ns = new NpcNamespace(tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
            ns.EnsureDirectories();
            var memoryManager = ns.CreateMemoryManager(new FakeChatClient(), NullLogger<MemoryManager>.Instance);
            var factStore = new NpcObservationFactStore();
            var adapter = new FakeGameAdapter(
                new CountingCommandService(),
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is idle.",
                    ["location=Town"])),
                new FakeEventSource([]));
            var agent = new FakeAgent(() => { }, "I will wait near the fountain.");
            var loop = new NpcAutonomyLoop(
                adapter,
                factStore,
                agent,
                memoryManager: memoryManager,
                traceIdFactory: () => "trace-memory");

            await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

            var entries = await memoryManager.ReadEntriesAsync("memory", CancellationToken.None);
            Assert.AreEqual(1, entries.Count);
            Assert.IsTrue(entries[0].Contains("trace-memory", StringComparison.Ordinal));
            Assert.IsTrue(entries[0].Contains("I will wait near the fountain.", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private static NpcRuntimeDescriptor CreateDescriptor(string npcId)
        => new(
            npcId,
            npcId,
            "stardew-valley",
            "save-1",
            "default",
            "stardew",
            "pack-root",
            $"sdv_save-1_{npcId}_default");

    private static IReadOnlyList<JsonElement> ReadRuntimeLogRecords(string logPath)
    {
        using var document = JsonDocument.Parse("[" + string.Join(",", File.ReadAllLines(logPath)) + "]");
        return document.RootElement.EnumerateArray()
            .Select(element => element.Clone())
            .ToArray();
    }

    private static void AssertRuntimeRecord(
        IReadOnlyList<JsonElement> records,
        string target,
        string stage,
        string result,
        string? commandId = null)
    {
        Assert.IsTrue(records.Any(record =>
            record.GetProperty("actionType").GetString() == "task_continuity" &&
            record.GetProperty("target").GetString() == target &&
            record.GetProperty("stage").GetString() == stage &&
            record.GetProperty("result").GetString() == result &&
            (commandId is null ||
             (record.TryGetProperty("commandId", out var commandProperty) &&
              commandProperty.GetString() == commandId))),
            $"Missing task_continuity record target={target}, stage={stage}, result={result}, commandId={commandId ?? "-"}.");
    }

    private sealed class FakeGameAdapter : IGameAdapter
    {
        public FakeGameAdapter(IGameCommandService commands, IGameQueryService queries, IGameEventSource events)
        {
            Commands = commands;
            Queries = queries;
            Events = events;
        }

        public string AdapterId => "stardew";

        public IGameCommandService Commands { get; }

        public IGameQueryService Queries { get; }

        public IGameEventSource Events { get; }
    }

    private sealed class CountingCommandService : IGameCommandService
    {
        public int SubmitCalls { get; private set; }
        public int StatusCalls { get; private set; }
        public int CancelCalls { get; private set; }
        public GameAction? LastSubmittedAction { get; private set; }

        public Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
        {
            SubmitCalls++;
            LastSubmittedAction = action;
            return Task.FromResult(new GameCommandResult(true, "cmd-fallback", "queued", null, action.TraceId));
        }

        public Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
        {
            StatusCalls++;
            return Task.FromResult(new GameCommandStatus(commandId, "", "", "not-called", 0, null, "unexpected"));
        }

        public Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
        {
            CancelCalls++;
            return Task.FromResult(new GameCommandStatus(commandId, "", "", "not-called", 0, null, "unexpected"));
        }
    }

    private sealed class FakeQueryService : IGameQueryService
    {
        private readonly GameObservation _observation;

        public FakeQueryService(GameObservation observation)
        {
            _observation = observation;
        }

        public string? LastNpcId { get; private set; }

        public Action? OnObserve { get; set; }

        public Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
        {
            OnObserve?.Invoke();
            LastNpcId = npcId;
            return Task.FromResult(_observation);
        }

        public Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new WorldSnapshot("stardew-valley", "save-1", _observation.TimestampUtc, [], []));
    }

    private sealed class FakeEventSource : IGameEventSource
    {
        private readonly GameEventBatch _batch;

        public FakeEventSource(IReadOnlyList<GameEventRecord> records)
        {
            _batch = new GameEventBatch(records, GameEventCursor.Advance(new GameEventCursor(), records));
        }

        public FakeEventSource(GameEventBatch batch)
        {
            _batch = batch;
        }

        public GameEventCursor? LastCursor { get; private set; }

        public Action? OnPoll { get; set; }

        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
        {
            OnPoll?.Invoke();
            LastCursor = cursor;
            return Task.FromResult(_batch.Records);
        }

        public Task<GameEventBatch> PollBatchAsync(GameEventCursor cursor, CancellationToken ct)
        {
            OnPoll?.Invoke();
            LastCursor = cursor;
            return Task.FromResult(_batch);
        }
    }

    private sealed class FakeAgent : Hermes.Agent.Core.IAgent
    {
        private readonly Action _onChat;

        private readonly string _response;
        private readonly Action<Session>? _mutateSession;

        public FakeAgent(Action onChat, string response = "wait", Action<Session>? mutateSession = null)
        {
            _onChat = onChat;
            _response = response;
            _mutateSession = mutateSession;
        }

        public int ChatCalls { get; private set; }

        public string? LastMessage { get; private set; }

        public List<string> Messages { get; } = new();

        public Task<string> ChatAsync(string message, Hermes.Agent.Core.Session session, CancellationToken ct)
        {
            _onChat();
            ChatCalls++;
            LastMessage = message;
            Messages.Add(message);
            _mutateSession?.Invoke(session);
            return Task.FromResult(_response);
        }

        public async IAsyncEnumerable<Hermes.Agent.LLM.StreamEvent> StreamChatAsync(
            string message,
            Hermes.Agent.Core.Session session,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public void RegisterTool(Hermes.Agent.Core.ITool tool)
        {
        }
    }

    private sealed class FakeChatClient : IChatClient
    {
        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("summary");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
            => Task.FromResult(new ChatResponse { Content = "ok", FinishReason = "stop" });

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
