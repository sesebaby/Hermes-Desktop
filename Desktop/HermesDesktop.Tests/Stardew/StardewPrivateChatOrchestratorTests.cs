using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public class StardewPrivateChatOrchestratorTests
{
    [TestMethod]
    public async Task ProcessNextAsync_HaleyVanillaDialogueCompleted_SubmitsOpenPrivateChat()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."));
        var commands = new FakeCommandService();
        var agent = new FakePrivateChatAgentRunner();
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            agent,
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.Never));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, commands.Submitted.Count);
        Assert.AreEqual(GameActionType.OpenPrivateChat, commands.Submitted[0].Type);
        Assert.AreEqual("Haley", commands.Submitted[0].NpcId);
        Assert.AreEqual("Haley", commands.Submitted[0].BodyBinding?.TargetEntityId);
        Assert.AreEqual("pc_evt-1", commands.Submitted[0].Payload?["conversationId"]?.GetValue<string>());
        Assert.AreEqual(0, agent.Requests.Count);
    }

    [TestMethod]
    public async Task ProcessNextAsync_UsesConfiguredBodyBindingWhenNpcIdIsLogical()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."));
        var commands = new FakeCommandService();
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new StardewPrivateChatOptions(
                NpcId: "haley",
                ReopenPolicy: PrivateChatReopenPolicy.Never,
                BodyBinding: new NpcBodyBinding("haley", "Haley", "Haley", "Haley", "stardew")));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual("haley", commands.Submitted.Single().NpcId);
        Assert.AreEqual("Haley", commands.Submitted.Single().BodyBinding?.TargetEntityId);
    }

    [TestMethod]
    public async Task RuntimeAdapter_NewBridgeDrainsFirstBatchBeforeProcessingNewEvents()
    {
        var events = new FakeEventSource();
        var commands = new FakeCommandService();
        using var runtimeAdapter = new StardewPrivateChatRuntimeAdapter(
            new FakePrivateChatAgentRunner(),
            NullLogger<StardewPrivateChatRuntimeAdapter>.Instance,
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.Never));
        var adapter = new FakeGameAdapter(commands, events);

        await runtimeAdapter.ProcessAsync(
            "bridge-1",
            "save-1",
            adapter,
            [
                new GameEventRecord(
                    "evt-old",
                    "vanilla_dialogue_completed",
                    "Haley",
                    DateTime.UtcNow,
                    "Historical Haley dialogue completed.")
            ],
            CancellationToken.None,
            drainOnly: true);

        Assert.AreEqual(0, commands.Submitted.Count);

        await runtimeAdapter.ProcessAsync(
            "bridge-1",
            "save-1",
            adapter,
            [
                new GameEventRecord(
                    "evt-new",
                    "vanilla_dialogue_completed",
                    "Haley",
                    DateTime.UtcNow.AddSeconds(1),
                    "Fresh Haley dialogue completed.")
            ],
            CancellationToken.None);

        Assert.AreEqual(1, commands.Submitted.Count);
        Assert.AreEqual(GameActionType.OpenPrivateChat, commands.Submitted[0].Type);
        Assert.AreEqual("pc_evt-new", commands.Submitted[0].Payload?["conversationId"]?.GetValue<string>());
    }

    [TestMethod]
    public async Task RuntimeAdapter_ResolvesPrivateChatBodyBindingFromManifest()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-private-chat-runtime-binding-tests", Guid.NewGuid().ToString("N"));
        try
        {
            CreatePack(tempDir, "haley", "Haley", targetEntityId: "Haley");
            var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), tempDir);
            var events = new FakeEventSource();
            var commands = new FakeCommandService();
            using var runtimeAdapter = new StardewPrivateChatRuntimeAdapter(
                new FakePrivateChatAgentRunner(),
                NullLogger<StardewPrivateChatRuntimeAdapter>.Instance,
                new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.Never),
                bindingResolver: resolver);
            var adapter = new FakeGameAdapter(commands, events);

            await runtimeAdapter.ProcessAsync(
                "bridge-1",
                "save-1",
                adapter,
                [
                    new GameEventRecord(
                        "evt-new",
                        "vanilla_dialogue_completed",
                        "haley",
                        DateTime.UtcNow,
                        "Fresh Haley dialogue completed.")
                ],
                CancellationToken.None);

            var action = commands.Submitted.Single();
            Assert.AreEqual("haley", action.NpcId);
            Assert.AreEqual("Haley", action.BodyBinding?.TargetEntityId);
            Assert.AreEqual("Haley", action.BodyBinding?.SmapiName);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessNextAsync_OtherNpcDialogueCompleted_DoesNotOpenPrivateChat()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Penny",
                DateTime.UtcNow,
                "Penny vanilla dialogue completed."));
        var commands = new FakeCommandService();
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.Never));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(0, commands.Submitted.Count);
    }

    [TestMethod]
    public async Task ProcessNextAsync_PennyDialogueCompleted_WithWildcardTarget_OpensPennyPrivateChat()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Penny",
                DateTime.UtcNow,
                "Penny vanilla dialogue completed."));
        var commands = new FakeCommandService();
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new StardewPrivateChatOptions(ReopenPolicy: PrivateChatReopenPolicy.Never));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, commands.Submitted.Count);
        Assert.AreEqual(GameActionType.OpenPrivateChat, commands.Submitted[0].Type);
        Assert.AreEqual("Penny", commands.Submitted[0].NpcId);
        Assert.AreEqual("pc_evt-1", commands.Submitted[0].Payload?["conversationId"]?.GetValue<string>());
        Assert.AreEqual(StardewPrivateChatState.AwaitingPlayerInput, orchestrator.State);
    }

    [TestMethod]
    public async Task ProcessNextAsync_HaleyDialogueUnavailable_SubmitsOpenPrivateChat()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_unavailable",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue follow-up was unavailable."));
        var commands = new FakeCommandService();
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.Never));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, commands.Submitted.Count);
        Assert.AreEqual(GameActionType.OpenPrivateChat, commands.Submitted[0].Type);
        Assert.AreEqual("pc_evt-1", commands.Submitted[0].Payload?["conversationId"]?.GetValue<string>());
        Assert.AreEqual(StardewPrivateChatState.AwaitingPlayerInput, orchestrator.State);
    }

    [TestMethod]
    public async Task ProcessNextAsync_DuplicateDialogueEvent_OpensPrivateChatOnce()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."),
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."));
        var commands = new FakeCommandService();
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.Never));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, commands.Submitted.Count);
    }

    [TestMethod]
    public async Task ProcessNextAsync_ActiveHaleySession_DoesNotOpenConcurrentPennySession()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."),
            new GameEventRecord(
                "evt-2",
                "vanilla_dialogue_completed",
                "Penny",
                DateTime.UtcNow.AddSeconds(1),
                "Penny vanilla dialogue completed."));
        var commands = new FakeCommandService();
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new StardewPrivateChatOptions(ReopenPolicy: PrivateChatReopenPolicy.Never));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, commands.Submitted.Count);
        Assert.AreEqual("Haley", commands.Submitted[0].NpcId);
        Assert.AreEqual(StardewPrivateChatState.AwaitingPlayerInput, orchestrator.State);
        Assert.AreEqual("pc_evt-1", orchestrator.ConversationId);
    }

    [TestMethod]
    public async Task ProcessNextAsync_AfterHaleySessionEnds_PennyCanOpenNextSession()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."),
            new GameEventRecord(
                "evt-2",
                "player_private_message_cancelled",
                "Haley",
                DateTime.UtcNow.AddSeconds(1),
                "Player cancelled Haley private chat.",
                "pc_evt-1",
                new JsonObject { ["conversationId"] = "pc_evt-1" }),
            new GameEventRecord(
                "evt-3",
                "vanilla_dialogue_completed",
                "Penny",
                DateTime.UtcNow.AddSeconds(2),
                "Penny vanilla dialogue completed."));
        var commands = new FakeCommandService();
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new StardewPrivateChatOptions(ReopenPolicy: PrivateChatReopenPolicy.Never));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(2, commands.Submitted.Count);
        Assert.AreEqual("Haley", commands.Submitted[0].NpcId);
        Assert.AreEqual("Penny", commands.Submitted[1].NpcId);
        Assert.AreEqual("pc_evt-3", commands.Submitted[1].Payload?["conversationId"]?.GetValue<string>());
        Assert.AreEqual(StardewPrivateChatState.AwaitingPlayerInput, orchestrator.State);
        Assert.AreEqual("pc_evt-3", orchestrator.ConversationId);
    }

    [TestMethod]
    public async Task ProcessNextAsync_PlayerPrivateMessageSubmitted_RoutesTextToAgentAndSpeaksReply()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."),
            new GameEventRecord(
                "evt-2",
                "player_private_message_submitted",
                "Haley",
                DateTime.UtcNow,
                "Player submitted a private chat message.",
                "pc_evt-1",
                new JsonObject
                {
                    ["conversationId"] = "pc_evt-1",
                    ["text"] = "hi Haley"
                }));
        var commands = new FakeCommandService();
        var agent = new FakePrivateChatAgentRunner { ReplyText = "Oh. Hi." };
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            agent,
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.Never));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, agent.Requests.Count);
        Assert.AreEqual("hi Haley", agent.Requests[0].PlayerText);
        Assert.AreEqual("pc_evt-1", agent.Requests[0].ConversationId);
        Assert.AreEqual(2, commands.Submitted.Count);
        Assert.AreEqual(GameActionType.Speak, commands.Submitted[1].Type);
        Assert.AreEqual("Oh. Hi.", commands.Submitted[1].Payload?["text"]?.GetValue<string>());
        Assert.AreEqual("input_menu", commands.Submitted[1].Payload?["source"]?.GetValue<string>());
    }

    [TestMethod]
    public async Task ProcessNextAsync_WhenAgentReplyThrows_SubmitsSystemErrorWithoutChangingDisplaySource()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."),
            new GameEventRecord(
                "evt-2",
                "player_private_message_submitted",
                "Haley",
                DateTime.UtcNow,
                "Player submitted a private chat message.",
                "pc_evt-1",
                new JsonObject
                {
                    ["conversationId"] = "pc_evt-1",
                    ["text"] = "hi Haley",
                    ["source"] = "input_menu"
                }));
        var commands = new FakeCommandService();
        var agent = new FakePrivateChatAgentRunner { ThrowOnReply = true };
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            agent,
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.Never));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, agent.Requests.Count);
        Assert.AreEqual(2, commands.Submitted.Count);
        Assert.AreEqual(GameActionType.OpenPrivateChat, commands.Submitted[0].Type);
        Assert.AreEqual(GameActionType.Speak, commands.Submitted[1].Type);
        Assert.AreEqual("input_menu", commands.Submitted[1].Payload?["source"]?.GetValue<string>());
        Assert.AreEqual("system_error", commands.Submitted[1].Payload?["message_kind"]?.GetValue<string>());
        StringAssert.Contains(
            commands.Submitted[1].Payload?["text"]?.GetValue<string>() ?? string.Empty,
            "AI connection failed");
    }

    [TestMethod]
    public async Task ProcessNextAsync_PhonePrivateMessageSubmitted_RoutesReplyWithPhoneOverlaySource()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."),
            new GameEventRecord(
                "evt-2",
                "player_private_message_submitted",
                "Haley",
                DateTime.UtcNow,
                "Player submitted a private chat message from phone.",
                "pc_evt-1",
                new JsonObject
                {
                    ["conversationId"] = "pc_evt-1",
                    ["text"] = "hi Haley",
                    ["source"] = "phone_overlay"
                }));
        var commands = new FakeCommandService();
        var agent = new FakePrivateChatAgentRunner { ReplyText = "Oh. Hi." };
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            agent,
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.Never));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(2, commands.Submitted.Count);
        Assert.AreEqual(GameActionType.Speak, commands.Submitted[1].Type);
        Assert.AreEqual("phone_overlay", commands.Submitted[1].Payload?["source"]?.GetValue<string>());
    }

    [TestMethod]
    public async Task ProcessNextAsync_OpenPrivateChatRetryableFailure_RetriesPendingOpenOnNextTick()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."));
        var commands = new FakeCommandService();
        commands.Results.Enqueue(new GameCommandResult(
            false,
            "",
            StardewCommandStatuses.Failed,
            StardewBridgeErrorCodes.MenuBlocked,
            "trace-open-1",
            Retryable: true));
        commands.Results.Enqueue(new GameCommandResult(
            true,
            "",
            StardewCommandStatuses.Completed,
            null,
            "trace-open-2"));
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.Never));

        await orchestrator.ProcessNextAsync(CancellationToken.None);
        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(2, commands.Submitted.Count);
        Assert.IsTrue(commands.Submitted.All(action => action.Type == GameActionType.OpenPrivateChat));
        Assert.AreEqual(StardewPrivateChatState.AwaitingPlayerInput, orchestrator.State);
        Assert.AreEqual("pc_evt-1", orchestrator.ConversationId);
    }

    [TestMethod]
    public async Task ProcessNextAsync_WorldNotReadyOpenFailure_RetriesByErrorCode()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."));
        var commands = new FakeCommandService();
        commands.Results.Enqueue(new GameCommandResult(
            false,
            "",
            StardewCommandStatuses.Failed,
            StardewBridgeErrorCodes.WorldNotReady,
            "trace-open-1"));
        commands.Results.Enqueue(new GameCommandResult(
            true,
            "",
            StardewCommandStatuses.Completed,
            null,
            "trace-open-2"));
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.Never));

        await orchestrator.ProcessNextAsync(CancellationToken.None);
        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(2, commands.Submitted.Count);
        Assert.AreEqual(StardewPrivateChatState.AwaitingPlayerInput, orchestrator.State);
    }

    [TestMethod]
    public async Task ProcessNextAsync_PrivateChatReplyClosed_ReopensAfterReplyInsteadOfImmediately()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."),
            new GameEventRecord(
                "evt-2",
                "player_private_message_submitted",
                "Haley",
                DateTime.UtcNow,
                "Player submitted a private chat message.",
                "pc_evt-1",
                new JsonObject
                {
                    ["conversationId"] = "pc_evt-1",
                    ["text"] = "hi Haley"
                }));
        var commands = new FakeCommandService();
        var agent = new FakePrivateChatAgentRunner { ReplyText = "Oh. Hi." };
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            agent,
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.OnceAfterReply, MaxTurnsPerSession: 2));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(2, commands.Submitted.Count);
        Assert.AreEqual(GameActionType.OpenPrivateChat, commands.Submitted[0].Type);
        Assert.AreEqual(GameActionType.Speak, commands.Submitted[1].Type);
        Assert.AreEqual(StardewPrivateChatState.WaitingReplyDismissal, orchestrator.State);

        events.Add(new GameEventRecord(
            "evt-3",
            "private_chat_reply_closed",
            "Haley",
            DateTime.UtcNow,
            "Haley private chat reply closed.",
            "pc_evt-1",
            new JsonObject { ["conversationId"] = "pc_evt-1" }));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(3, commands.Submitted.Count);
        Assert.AreEqual(GameActionType.OpenPrivateChat, commands.Submitted[2].Type);
        Assert.AreEqual("pc_evt-1_turn2", commands.Submitted[2].Payload?["conversationId"]?.GetValue<string>());
        Assert.AreEqual(StardewPrivateChatState.AwaitingPlayerInput, orchestrator.State);
    }

    [TestMethod]
    public async Task ProcessNextAsync_PlayerPrivateMessageCancelled_EndsSession()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."),
            new GameEventRecord(
                "evt-2",
                "player_private_message_cancelled",
                "Haley",
                DateTime.UtcNow,
                "Player cancelled private chat.",
                "pc_evt-1",
                new JsonObject { ["conversationId"] = "pc_evt-1" }));
        var commands = new FakeCommandService();
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.OnceAfterReply));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(StardewPrivateChatState.Idle, orchestrator.State);
        Assert.IsNull(orchestrator.ConversationId);
    }

    [TestMethod]
    public async Task ProcessNextAsync_PlayerPrivateMessageCancelled_AllowsUnavailableClickToOpenNewChat()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."),
            new GameEventRecord(
                "evt-2",
                "player_private_message_cancelled",
                "Haley",
                DateTime.UtcNow,
                "Player closed private chat without submitting.",
                "pc_evt-1",
                new JsonObject { ["conversationId"] = "pc_evt-1" }),
            new GameEventRecord(
                "evt-3",
                "vanilla_dialogue_unavailable",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue follow-up was unavailable."));
        var commands = new FakeCommandService();
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.OnceAfterReply));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(2, commands.Submitted.Count);
        Assert.AreEqual(GameActionType.OpenPrivateChat, commands.Submitted[0].Type);
        Assert.AreEqual(GameActionType.OpenPrivateChat, commands.Submitted[1].Type);
        Assert.AreEqual("pc_evt-3", commands.Submitted[1].Payload?["conversationId"]?.GetValue<string>());
        Assert.AreEqual(StardewPrivateChatState.AwaitingPlayerInput, orchestrator.State);
    }

    [TestMethod]
    public async Task ProcessNextAsync_OrdinaryEvent_DoesNotCallAgent()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "proximity",
                "Haley",
                DateTime.UtcNow,
                "The farmer entered Haley's proximity."));
        var commands = new FakeCommandService();
        var agent = new FakePrivateChatAgentRunner();
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            agent,
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.Never));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(0, commands.Submitted.Count);
        Assert.AreEqual(0, agent.Requests.Count);
    }

    [TestMethod]
    public async Task Dispose_AfterOpenDoesNotReleaseSessionLeaseBecausePhoneThreadIsPassive()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."));
        var commands = new FakeCommandService();
        var leases = new FakePrivateChatSessionLeaseCoordinator();
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.Never),
            leases);

        await orchestrator.ProcessNextAsync(CancellationToken.None);
        orchestrator.Dispose();

        Assert.AreEqual(0, leases.AcquireCalls.Count);
        Assert.AreEqual(0, leases.ReleaseCalls.Count);
        Assert.AreEqual(0, leases.ActiveLeaseCount);
    }

    [TestMethod]
    public async Task ProcessNextAsync_PlayerPrivateMessageSubmitted_AcquiresAndReleasesSessionLeaseForReplyOnly()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."),
            new GameEventRecord(
                "evt-2",
                "player_private_message_submitted",
                "Haley",
                DateTime.UtcNow.AddSeconds(1),
                "Player submitted a private chat message.",
                "pc_evt-1",
                new JsonObject
                {
                    ["conversationId"] = "pc_evt-1",
                    ["text"] = "hi Haley"
                }));
        var commands = new FakeCommandService();
        var leases = new FakePrivateChatSessionLeaseCoordinator();
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner { ReplyText = "Oh. Hi." },
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.OnceAfterReply),
            leases);

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, leases.AcquireCalls.Count);
        Assert.AreEqual("Haley", leases.AcquireCalls[0].NpcId);
        Assert.AreEqual("pc_evt-1", leases.AcquireCalls[0].ConversationId);
        Assert.AreEqual(1, leases.ReleaseCalls.Count);
        Assert.AreEqual("pc_evt-1", leases.ReleaseCalls[0].ConversationId);
        Assert.AreEqual(0, leases.ActiveLeaseCount);
        Assert.AreEqual(StardewPrivateChatState.WaitingReplyDismissal, orchestrator.State);
    }

    [TestMethod]
    public async Task DrainExistingEventsAsync_AdvancesCursorWithoutOpeningStaleChat()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-1",
                "vanilla_dialogue_completed",
                "Haley",
                DateTime.UtcNow,
                "Haley vanilla dialogue completed."));
        var commands = new FakeCommandService();
        var orchestrator = new StardewPrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new StardewPrivateChatOptions(NpcId: "haley", ReopenPolicy: PrivateChatReopenPolicy.Never));

        await orchestrator.DrainExistingEventsAsync(CancellationToken.None);
        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(0, commands.Submitted.Count);
    }

    private sealed class FakeEventSource : IGameEventSource
    {
        private readonly List<GameEventRecord> _records;

        public FakeEventSource(params GameEventRecord[] records)
        {
            _records = records.ToList();
        }

        public void Add(params GameEventRecord[] records)
        {
            _records.AddRange(records);
        }

        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
        {
            var start = 0;
            if (!string.IsNullOrWhiteSpace(cursor.Since))
            {
                var index = _records.ToList().FindIndex(record => string.Equals(record.EventId, cursor.Since, StringComparison.OrdinalIgnoreCase));
                start = index < 0 ? _records.Count : index + 1;
            }

            var records = _records.Skip(start).ToArray();
            return Task.FromResult<IReadOnlyList<GameEventRecord>>(records);
        }
    }

    private static void CreatePack(string root, string npcId, string displayName, string? targetEntityId = null)
    {
        var packRoot = Path.Combine(root, npcId, "default");
        Directory.CreateDirectory(packRoot);
        foreach (var file in new[] { "SOUL.md", "facts.md", "voice.md", "boundaries.md", "skills.json" })
            File.WriteAllText(Path.Combine(packRoot, file), file == "skills.json" ? """{"required":[],"optional":[]}""" : "ok");

        var manifest = new NpcPackManifest
        {
            SchemaVersion = 1,
            NpcId = npcId,
            GameId = "stardew-valley",
            ProfileId = "default",
            DefaultProfileId = "default",
            DisplayName = displayName,
            SmapiName = displayName,
            Aliases = [npcId, displayName],
            TargetEntityId = targetEntityId ?? npcId,
            AdapterId = "stardew",
            SoulFile = "SOUL.md",
            FactsFile = "facts.md",
            VoiceFile = "voice.md",
            BoundariesFile = "boundaries.md",
            SkillsFile = "skills.json",
            Capabilities = ["move", "speak"]
        };
        File.WriteAllText(Path.Combine(packRoot, FileSystemNpcPackLoader.ManifestFileName), JsonSerializer.Serialize(manifest));
    }

    private sealed class FakeCommandService : IGameCommandService
    {
        public List<GameAction> Submitted { get; } = new();

        public Queue<GameCommandResult> Results { get; } = new();

        public Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
        {
            Submitted.Add(action);
            if (Results.TryDequeue(out var result))
                return Task.FromResult(result);

            return Task.FromResult(new GameCommandResult(true, "", StardewCommandStatuses.Completed, null, action.TraceId));
        }

        public Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "Haley", "private_chat", StardewCommandStatuses.Completed, 1, null, null));

        public Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "Haley", "private_chat", StardewCommandStatuses.Cancelled, 1, reason, null));
    }

    private sealed class FakePrivateChatAgentRunner : INpcPrivateChatAgentRunner
    {
        public List<NpcPrivateChatRequest> Requests { get; } = new();
        public string ReplyText { get; init; } = "Hello.";
        public bool ThrowOnReply { get; init; }

        public Task<NpcPrivateChatReply> ReplyAsync(NpcPrivateChatRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            if (ThrowOnReply)
                throw new InvalidOperationException("provider unavailable");

            return Task.FromResult(new NpcPrivateChatReply(ReplyText));
        }
    }

    private sealed class FakeGameAdapter : IGameAdapter
    {
        public FakeGameAdapter(IGameCommandService commands, IGameEventSource events)
        {
            Commands = commands;
            Events = events;
            Queries = new NoopQueryService();
        }

        public string AdapterId => "stardew";

        public IGameCommandService Commands { get; }

        public IGameQueryService Queries { get; }

        public IGameEventSource Events { get; }
    }

    private sealed class NoopQueryService : IGameQueryService
    {
        public Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new GameObservation(npcId, "stardew-valley", DateTime.UtcNow, $"{npcId} idle", []));

        public Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new WorldSnapshot("stardew-valley", "save-1", DateTime.UtcNow, [], []));
    }

    private sealed class FakePrivateChatSessionLeaseCoordinator : IPrivateChatSessionLeaseCoordinator
    {
        public List<PrivateChatSessionLeaseRequest> AcquireCalls { get; } = new();

        public List<ReleasedLease> ReleaseCalls { get; } = new();

        public int ActiveLeaseCount { get; private set; }

        public Task<IPrivateChatSessionLease> AcquireAsync(PrivateChatSessionLeaseRequest request, CancellationToken ct)
        {
            AcquireCalls.Add(request);
            ActiveLeaseCount++;
            return Task.FromResult<IPrivateChatSessionLease>(new FakePrivateChatSessionLease(this, request, AcquireCalls.Count));
        }

        private sealed class FakePrivateChatSessionLease : IPrivateChatSessionLease
        {
            private readonly FakePrivateChatSessionLeaseCoordinator _owner;
            private bool _disposed;

            public FakePrivateChatSessionLease(
                FakePrivateChatSessionLeaseCoordinator owner,
                PrivateChatSessionLeaseRequest request,
                int generation)
            {
                _owner = owner;
                NpcId = request.NpcId;
                ConversationId = request.ConversationId;
                Owner = request.Owner;
                Generation = generation;
            }

            public string NpcId { get; }

            public string ConversationId { get; }

            public string Owner { get; }

            public int Generation { get; }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _owner.ActiveLeaseCount--;
                _owner.ReleaseCalls.Add(new ReleasedLease(ConversationId, Generation, Owner));
            }
        }

        public sealed record ReleasedLease(string ConversationId, int Generation, string Owner);
    }
}
