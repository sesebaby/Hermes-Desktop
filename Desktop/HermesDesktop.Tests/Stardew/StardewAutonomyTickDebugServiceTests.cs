using System.Runtime.CompilerServices;
using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public sealed class StardewAutonomyTickDebugServiceTests
{
    private string _tempDir = null!;
    private string _packRoot = null!;
    private string _gamingSkillRoot = null!;
    private SkillManager _skillManager = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-autonomy-tests", Guid.NewGuid().ToString("N"));
        _packRoot = Path.Combine(_tempDir, "packs");
        _gamingSkillRoot = Path.Combine(_tempDir, "skills", "gaming");

        var skillsDir = Path.Combine(_tempDir, "skills", "autonomy");
        Directory.CreateDirectory(skillsDir);
        CreateGamingSkillFixtures(_gamingSkillRoot);
        File.WriteAllText(
            Path.Combine(skillsDir, "SKILL.md"),
            """
            ---
            name: npc-autonomy-skill
            description: Keep autonomy actions grounded in observed Stardew facts.
            ---
            Use game facts before taking autonomous action.
            """);
        _skillManager = new SkillManager(Path.Combine(_tempDir, "skills"), NullLogger<SkillManager>.Instance);

        CreatePack("haley", "Haley");
        CreatePack("penny", "Penny");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task RunOneTickAsync_HaleyInjectsRequiredPersonaSkillsAndPreservesStardewTools()
    {
        var chatClient = new ToolSnapshotChatClient("I will wait near the library.");
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            DateTime.UtcNow,
            "Haley is near the fountain.",
            ["location=Town", "tile=12,8"]));
        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            queries,
            new FakeEventSource([
                new GameEventRecord("evt-1", "time_changed", null, DateTime.UtcNow, "The clock advanced.")
            ]));
        var discovery = new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
            new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
            DateTimeOffset.UtcNow,
            1234,
            "save-42"));
        var service = new StardewAutonomyTickDebugService(
            discovery,
            _ => adapter,
            chatClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            CreatePromptSupplementBuilder(),
            discoveredToolProvider: () => [new DiscoveredNoopTool("mcp_dynamic_test")],
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Haley", CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("haley", result.NpcId);
        Assert.AreEqual("I will wait near the library.", result.DecisionResponse);
        Assert.AreEqual("haley", queries.LastNpcId);

        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "memory");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "session_search");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "skills_list");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "skill_invoke");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "stardew_status");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "stardew_move");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "stardew_speak");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "stardew_open_private_chat");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "stardew_task_status");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "mcp_dynamic_test");

        Assert.IsTrue(chatClient.SystemMessages.Any(message =>
            message.Contains("## Skills (mandatory)", StringComparison.Ordinal) &&
            message.Contains("npc-autonomy-skill", StringComparison.Ordinal)));
        Assert.IsTrue(chatClient.SystemMessages.Any(message =>
            message.Contains("## Persona Facts", StringComparison.Ordinal) &&
            message.Contains("Haley facts", StringComparison.Ordinal) &&
            message.Contains("## Persona Voice", StringComparison.Ordinal) &&
            message.Contains("Haley voice", StringComparison.Ordinal) &&
            message.Contains("## Persona Boundaries", StringComparison.Ordinal) &&
            message.Contains("Haley boundaries", StringComparison.Ordinal) &&
            message.Contains("## Stardew Required Skills", StringComparison.Ordinal) &&
            message.Contains("stardew-core test guidance", StringComparison.Ordinal) &&
            message.Contains("stardew-social test guidance", StringComparison.Ordinal) &&
            message.Contains("stardew-navigation test guidance", StringComparison.Ordinal) &&
            message.Contains("stardew-task-continuity test guidance", StringComparison.Ordinal) &&
            message.Contains("stardew-world test guidance", StringComparison.Ordinal) &&
            message.Contains("references/stardew-places.md", StringComparison.Ordinal) &&
            !message.Contains("full stardew places encyclopedia fixture", StringComparison.Ordinal)));

        var runtimeSoulPath = Path.Combine(
            _tempDir,
            "runtime",
            "stardew",
            "games",
            "stardew-valley",
            "saves",
            "save-42",
            "npc",
            "haley",
            "profiles",
            "default",
            "SOUL.md");
        Assert.IsTrue(File.Exists(runtimeSoulPath));
        StringAssert.Contains(await File.ReadAllTextAsync(runtimeSoulPath), "haley-pack-soul");
    }

    [TestMethod]
    public async Task RunOneTickAsync_WithRepositoryGamingSkillRootInjectsWorldAndNavigationOwnerGuidance()
    {
        var repositoryGamingRoot = FindRepositoryPath("skills", "gaming");
        var chatClient = new ToolSnapshotChatClient("I will observe before moving.");
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            DateTime.UtcNow,
            "Haley is in her room.",
            [
                "location=HaleyHouse",
                "tile=8,7",
                "destination[0]=label=Bedroom mirror,locationName=HaleyHouse,x=6,y=4,tags=home|photogenic,reason=check her look before going out"
            ]));
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                "save-42")),
            _ => new FakeGameAdapter(new FakeCommandService(), queries, new FakeEventSource([])),
            chatClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            CreatePromptSupplementBuilder(new FixedStardewGamingSkillRootProvider(repositoryGamingRoot)),
            discoveredToolProvider: null,
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Haley", CancellationToken.None);

        Assert.IsTrue(result.Success, result.FailureReason);
        var systemPrompt = chatClient.SystemMessages.First(message =>
            message.Contains("## Stardew Required Skills", StringComparison.Ordinal));
        StringAssert.Contains(systemPrompt, "### stardew-world");
        StringAssert.Contains(systemPrompt, "本 skill 是地点意义与候选解释 owner");
        StringAssert.Contains(systemPrompt, "`destination[n]` 的 `destinationId`、`label`、`tags`、`reason`、`endBehavior`");
        StringAssert.Contains(systemPrompt, "### stardew-navigation");
        StringAssert.Contains(systemPrompt, "This skill owns the move loop");
        StringAssert.Contains(systemPrompt, "observe destinations");
        StringAssert.Contains(systemPrompt, "choose one matching intent");
        StringAssert.Contains(systemPrompt, "`stardew_move(destination, reason)`");
        StringAssert.Contains(systemPrompt, "destination=<exact destinationId from destination[n]>");
        StringAssert.Contains(systemPrompt, "Movement Is Not Narration Text");
        StringAssert.Contains(systemPrompt, "MUST call `stardew_move`");
        Assert.IsFalse(
            systemPrompt.Contains("stardew-world test guidance", StringComparison.Ordinal),
            "Repo-backed prompt supplement coverage must use the real skills/gaming root, not fixture-only text.");
    }

    [TestMethod]
    public async Task RunOneTickAsync_WithRepositoryGamingSkillRootInjectsVisibleAutonomyMessageGuidance()
    {
        var repositoryGamingRoot = FindRepositoryPath("skills", "gaming");
        var chatClient = new ToolSnapshotChatClient("I will keep the player in the loop.");
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                "save-42")),
            _ => new FakeGameAdapter(
                new FakeCommandService(),
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is idling after a movement loop.",
                    [
                        "location=HaleyHouse",
                        "tile=8,7",
                        "isAvailableForControl=true"
                    ])),
                new FakeEventSource([])),
            chatClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            CreatePromptSupplementBuilder(new FixedStardewGamingSkillRootProvider(repositoryGamingRoot)),
            discoveredToolProvider: null,
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Haley", CancellationToken.None);

        Assert.IsTrue(result.Success, result.FailureReason);
        var systemPrompt = chatClient.SystemMessages.First(message =>
            message.Contains("## Stardew Required Skills", StringComparison.Ordinal));
        StringAssert.Contains(systemPrompt, "### stardew-social");
        StringAssert.Contains(systemPrompt, "如果连续多轮只移动、观察或查任务状态");
        StringAssert.Contains(systemPrompt, "`stardew_speak`");
        StringAssert.Contains(systemPrompt, "手机");
        StringAssert.Contains(systemPrompt, "头顶气泡");
        StringAssert.Contains(systemPrompt, "TheStardewSquad");
        StringAssert.Contains(systemPrompt, "MoveStarted");
        StringAssert.Contains(systemPrompt, "MoveArrived");
        StringAssert.Contains(systemPrompt, "Idle");
        StringAssert.Contains(systemPrompt, "TaskStatus");
        StringAssert.Contains(systemPrompt, "移动完成后一轮内");
    }

    [TestMethod]
    public async Task RunOneTickAsync_WithRepositoryGamingSkillRootInjectsChineseTaskContinuityGuidance()
    {
        var repositoryGamingRoot = FindRepositoryPath("skills", "gaming");
        var chatClient = new ToolSnapshotChatClient("我会把答应过的事接着做。");
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                "save-42")),
            _ => new FakeGameAdapter(
                new FakeCommandService(),
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is idle and can decide what to continue.",
                    [
                        "location=Town",
                        "tile=42,17",
                        "isAvailableForControl=true"
                    ])),
                new FakeEventSource([])),
            chatClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            CreatePromptSupplementBuilder(new FixedStardewGamingSkillRootProvider(repositoryGamingRoot)),
            discoveredToolProvider: null,
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Haley", CancellationToken.None);

        Assert.IsTrue(result.Success, result.FailureReason);
        var systemPrompt = chatClient.SystemMessages.First(message =>
            message.Contains("## Stardew Required Skills", StringComparison.Ordinal));
        StringAssert.Contains(systemPrompt, "### stardew-task-continuity");
        StringAssert.Contains(systemPrompt, "玩家给你以后要兑现的约定");
        StringAssert.Contains(systemPrompt, "先回应玩家，再恢复原来的任务");
        StringAssert.Contains(systemPrompt, "stardew_task_status");
        StringAssert.Contains(systemPrompt, "memory");
        StringAssert.Contains(systemPrompt, "session_search");
        Assert.IsFalse(
            systemPrompt.Contains("mc ", StringComparison.Ordinal),
            "Stardew continuity guidance must borrow HermesCraft's behavior pattern without leaking Minecraft command text.");
    }

    [TestMethod]
    public async Task RunOneTickAsync_WhenPackPromptFilesChange_RebindsWithLatestPersonaAndRequiredSkills()
    {
        var chatClient = new ToolSnapshotChatClient("I will wait near the library.");
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            DateTime.UtcNow,
            "Haley is near the fountain.",
            ["location=Town"]));
        var supervisor = new NpcRuntimeSupervisor();
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                "save-42")),
            _ => new FakeGameAdapter(new FakeCommandService(), queries, new FakeEventSource([])),
            chatClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            supervisor,
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            CreatePromptSupplementBuilder(),
            discoveredToolProvider: () => [new DiscoveredNoopTool("mcp_dynamic_test")],
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var first = await service.RunOneTickAsync("Haley", CancellationToken.None);
        Assert.IsTrue(first.Success);
        Assert.AreEqual(1, supervisor.Snapshot().Single().AutonomyRebindGeneration);

        var packRoot = Path.Combine(_packRoot, "haley", "default");
        File.WriteAllText(Path.Combine(packRoot, "facts.md"), "Haley updated facts");
        File.WriteAllText(
            Path.Combine(packRoot, "skills.json"),
            """{"required":["stardew-core","stardew-gossip"],"optional":[]}""");
        File.WriteAllText(Path.Combine(_gamingSkillRoot, "stardew-gossip.md"), "stardew-gossip updated guidance");

        var second = await service.RunOneTickAsync("Haley", CancellationToken.None);

        Assert.IsTrue(second.Success);
        Assert.AreEqual(2, supervisor.Snapshot().Single().AutonomyRebindGeneration);
        Assert.IsTrue(chatClient.SystemMessages.Any(message =>
            message.Contains("Haley updated facts", StringComparison.Ordinal) &&
            message.Contains("stardew-gossip updated guidance", StringComparison.Ordinal)));
        Assert.IsFalse(chatClient.SystemMessages.Any(message => message.Contains("Haley facts", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunOneTickAsync_WhenActiveGamingRootOnlyHasDescription_UsesBundledFallbackStardewSkills()
    {
        var activeGamingRoot = Path.Combine(_tempDir, "active-skills", "gaming");
        Directory.CreateDirectory(activeGamingRoot);
        File.WriteAllText(Path.Combine(activeGamingRoot, "DESCRIPTION.md"), "Gaming skill category description.");
        var chatClient = new ToolSnapshotChatClient("I will walk somewhere quiet.");
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            DateTime.UtcNow,
            "Haley is near the fountain.",
            ["location=Town"]));
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                "save-42")),
            _ => new FakeGameAdapter(new FakeCommandService(), queries, new FakeEventSource([])),
            chatClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            CreatePromptSupplementBuilder(new CompositeStardewGamingSkillRootProvider(activeGamingRoot, _gamingSkillRoot)),
            discoveredToolProvider: null,
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Haley", CancellationToken.None);

        Assert.IsTrue(result.Success, result.FailureReason);
        Assert.IsTrue(chatClient.SystemMessages.Any(message =>
            message.Contains("## Stardew Required Skills", StringComparison.Ordinal) &&
            message.Contains("stardew-core test guidance", StringComparison.Ordinal) &&
            message.Contains("stardew-social test guidance", StringComparison.Ordinal) &&
            message.Contains("stardew-navigation test guidance", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunOneTickAsync_MissingRequiredSkillFileFailsWithSkillIdAndPath()
    {
        File.Delete(Path.Combine(_gamingSkillRoot, "stardew-navigation.md"));
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                "save-42")),
            _ => new FakeGameAdapter(
                new FakeCommandService(),
                new FakeQueryService(new GameObservation("haley", "stardew-valley", DateTime.UtcNow, "Haley is idle.", [])),
                new FakeEventSource([])),
            new ToolSnapshotChatClient("unused"),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            CreatePromptSupplementBuilder(),
            discoveredToolProvider: null,
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Haley", CancellationToken.None);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.FailureReason, "haley");
        StringAssert.Contains(result.FailureReason, "stardew-navigation");
        StringAssert.Contains(result.FailureReason, Path.Combine(_gamingSkillRoot, "stardew-navigation.md"));
    }

    [TestMethod]
    public async Task RunOneTickAsync_InvalidSkillsJsonFailsWithSkillsJsonPath()
    {
        var skillsPath = Path.Combine(_packRoot, "haley", "default", "skills.json");
        File.WriteAllText(skillsPath, "[]");
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                "save-42")),
            _ => new FakeGameAdapter(
                new FakeCommandService(),
                new FakeQueryService(new GameObservation("haley", "stardew-valley", DateTime.UtcNow, "Haley is idle.", [])),
                new FakeEventSource([])),
            new ToolSnapshotChatClient("unused"),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            CreatePromptSupplementBuilder(),
            discoveredToolProvider: null,
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Haley", CancellationToken.None);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.FailureReason, "skills.json");
        StringAssert.Contains(
            result.FailureReason,
            Path.Combine(_packRoot, "haley", "default", "skills.json"));
    }

    [TestMethod]
    public async Task RunOneTickAsync_MissingPersonaFileFailsWithFilePath()
    {
        var factsPath = Path.Combine(_packRoot, "haley", "default", "facts.md");
        File.Delete(factsPath);
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                "save-42")),
            _ => throw new AssertFailedException("Adapter factory should not run when persona files are missing."),
            new ToolSnapshotChatClient("unused"),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            CreatePromptSupplementBuilder(),
            discoveredToolProvider: null,
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Haley", CancellationToken.None);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.FailureReason, "facts.md");
        StringAssert.Contains(result.FailureReason, factsPath);
    }

    [TestMethod]
    public async Task RunOneTickAsync_MissingNpcIdFailsInsteadOfDefaultingToHaley()
    {
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                "save-42")),
            _ => throw new AssertFailedException("Factory should not run when npcId is missing."),
            new ToolSnapshotChatClient("unused"),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            CreatePromptSupplementBuilder(),
            discoveredToolProvider: null,
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("", CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(StardewBridgeErrorCodes.InvalidTarget, result.FailureReason);
    }

    [TestMethod]
    public async Task RunOneTickAsync_MissingDiscoverySaveIdFailsInsteadOfUsingManualDebug()
    {
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                null)),
            _ => throw new AssertFailedException("Factory should not run when discovery saveId is missing."),
            new ToolSnapshotChatClient("unused"),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            new StardewNpcAutonomyPromptSupplementBuilder(new FixedStardewGamingSkillRootProvider(_gamingSkillRoot)),
            discoveredToolProvider: null,
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Penny", CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(StardewBridgeErrorCodes.BridgeStaleDiscovery, result.FailureReason);
    }

    [TestMethod]
    public async Task RunOneTickAsync_UsesPersistedRuntimeCursorInsteadOfRestartingFromRoot()
    {
        var chatClient = new ToolSnapshotChatClient("I will wait near the library.");
        var events = new CursorAwareEventSource([
            new GameEventRecord("evt-15", "time_changed", "penny", DateTime.UtcNow, "The clock advanced.", Sequence: 15)
        ]);
        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            new FakeQueryService(new GameObservation(
                "penny",
                "stardew-valley",
                DateTime.UtcNow,
                "Penny is near the library.",
                ["location=Town"])),
            events);
        var discovery = new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
            new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
            DateTimeOffset.UtcNow,
            1234,
            "save-42"));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("penny", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetControllerStateAsync(new GameEventCursor("evt-11", 14), null, CancellationToken.None);
        var service = new StardewAutonomyTickDebugService(
            discovery,
            _ => adapter,
            chatClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            supervisor,
            resolver,
            CreatePromptSupplementBuilder(),
            discoveredToolProvider: () => [new DiscoveredNoopTool("mcp_dynamic_test")],
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Penny", CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(14L, events.SeenCursor?.Sequence);
    }

    [TestMethod]
    public async Task RunOneTickAsync_WithObservedMoveCandidateExecutesStardewMoveTool()
    {
        var commands = new FakeCommandService();
        var chatClient = new ToolSnapshotChatClient(
            new ChatResponse
            {
                FinishReason = "tool_calls",
                ToolCalls =
                [
                    new ToolCall
                    {
                        Id = "call-move",
                        Name = "stardew_move",
                        Arguments = """{"destination":"Town fountain","reason":"stand somewhere bright and visible in town"}"""
                    }
                ]
            },
            new ChatResponse { Content = "Moved toward the Town fountain.", FinishReason = "stop" });
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            DateTime.UtcNow,
            "Haley is idle in town.",
            [
                "location=Town",
                "tile=42,17",
                "isAvailableForControl=true",
                "destination[0]=label=Town fountain,locationName=Town,x=42,y=17,tags=public|photogenic,reason=stand somewhere bright and visible in town,facingDirection=2",
                "nearby[0]=locationName=Town,x=43,y=17,reason=same_location_safe_reposition"
            ]));
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                "save-42")),
            _ => new FakeGameAdapter(commands, queries, new FakeEventSource([])),
            chatClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            CreatePromptSupplementBuilder(),
            discoveredToolProvider: null,
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Haley", CancellationToken.None);

        Assert.IsTrue(result.Success, result.FailureReason);
        Assert.AreEqual("Moved toward the Town fountain.", result.DecisionResponse);
        Assert.IsTrue(
            chatClient.UserMessages.Any(message => message.Contains("destination[0]=label=Town fountain", StringComparison.Ordinal)),
            "The model-facing autonomy prompt must expose the current destinations before the tool call happens.");
        Assert.IsNotNull(commands.LastAction);
        Assert.AreEqual(GameActionType.Move, commands.LastAction.Type);
        Assert.AreEqual("Town", commands.LastAction.Target.LocationName);
        Assert.AreEqual(42, commands.LastAction.Target.Tile?.X);
        Assert.AreEqual(17, commands.LastAction.Target.Tile?.Y);
    }

    [TestMethod]
    public async Task RunOneTickAsync_WithPlaceCandidateButNoMoveToolCall_DoesNotMove()
    {
        var commands = new FakeCommandService();
        var chatClient = new ToolSnapshotChatClient("I'll look around first.");
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            DateTime.UtcNow,
            "Haley is in her room.",
            [
                "location=HaleyHouse",
                "tile=8,7",
                "isAvailableForControl=true",
                "destination[0]=label=Bedroom mirror,locationName=HaleyHouse,x=6,y=4,tags=home|photogenic,reason=check her look before going out"
            ]));
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                "save-42")),
            _ => new FakeGameAdapter(commands, queries, new FakeEventSource([])),
            chatClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            CreatePromptSupplementBuilder(),
            discoveredToolProvider: null,
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Haley", CancellationToken.None);

        Assert.IsTrue(result.Success, result.FailureReason);
        Assert.AreEqual("I'll look around first.", result.DecisionResponse);
        Assert.IsTrue(
            chatClient.UserMessages.Any(message => message.Contains("destination[0]=label=Bedroom mirror", StringComparison.Ordinal)),
            "The candidate should be visible to the model, not converted into a host-side movement.");
        Assert.IsNull(commands.LastAction, "A placeCandidate fact must not force host-side movement without an agent tool call.");
    }

    [TestMethod]
    public async Task RunOneTickAsync_NonPrivateChatEventDoesNotDirectlyDriveMove()
    {
        var commands = new FakeCommandService();
        var chatClient = new ToolSnapshotChatClient("Noted.");
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            DateTime.UtcNow,
            "Haley is idle in town.",
            [
                "location=Town",
                "tile=42,17",
                "isAvailableForControl=true",
                "destination[0]=label=Town fountain,locationName=Town,x=42,y=20,tags=public|photogenic,reason=stand somewhere pretty"
            ]));
        var events = new FakeEventSource([
            new GameEventRecord("evt-1", "player_nearby", "haley", DateTime.UtcNow, "The player walked near Haley.")
        ]);
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                "save-42")),
            _ => new FakeGameAdapter(commands, queries, events),
            chatClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            CreatePromptSupplementBuilder(),
            discoveredToolProvider: null,
            worldCoordination: CreateWorldCoordination(),
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Haley", CancellationToken.None);

        Assert.IsTrue(result.Success, result.FailureReason);
        Assert.IsTrue(
            chatClient.UserMessages.Any(message =>
                message.Contains("[event] evt-1", StringComparison.Ordinal) &&
                message.Contains("下面的事件只是上下文", StringComparison.Ordinal)));
        Assert.IsNull(commands.LastAction, "Non-private-chat events must not directly drive host-side movement.");
    }

    private void CreatePack(string npcId, string displayName)
    {
        var root = Path.Combine(_packRoot, npcId, "default");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "SOUL.md"), $"# {displayName}\n\n{npcId}-pack-soul");
        File.WriteAllText(Path.Combine(root, "facts.md"), $"{displayName} facts");
        File.WriteAllText(Path.Combine(root, "voice.md"), $"{displayName} voice");
        File.WriteAllText(Path.Combine(root, "boundaries.md"), $"{displayName} boundaries");
        File.WriteAllText(
            Path.Combine(root, "skills.json"),
            """{"required":["stardew-core","stardew-social","stardew-navigation","stardew-task-continuity","stardew-world"],"optional":[]}""");

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
            TargetEntityId = npcId,
            AdapterId = "stardew",
            SoulFile = "SOUL.md",
            FactsFile = "facts.md",
            VoiceFile = "voice.md",
            BoundariesFile = "boundaries.md",
            SkillsFile = "skills.json",
            Capabilities = ["move", "speak"]
        };
        File.WriteAllText(
            Path.Combine(root, FileSystemNpcPackLoader.ManifestFileName),
            JsonSerializer.Serialize(manifest));
    }

    private static WorldCoordinationService CreateWorldCoordination()
        => new(new ResourceClaimRegistry());

    private StardewNpcAutonomyPromptSupplementBuilder CreatePromptSupplementBuilder(IStardewGamingSkillRootProvider? provider = null)
        => new(provider ?? new FixedStardewGamingSkillRootProvider(_gamingSkillRoot));

    private static string FindRepositoryPath(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativePath).ToArray());
            if (Directory.Exists(candidate) || File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        Assert.Fail($"Could not find repository path: {Path.Combine(relativePath)}");
        return string.Empty;
    }

    private static void CreateGamingSkillFixtures(string gamingSkillRoot)
    {
        Directory.CreateDirectory(gamingSkillRoot);
        File.WriteAllText(Path.Combine(gamingSkillRoot, "stardew-core.md"), "stardew-core test guidance");
        File.WriteAllText(Path.Combine(gamingSkillRoot, "stardew-social.md"), "stardew-social test guidance");
        File.WriteAllText(Path.Combine(gamingSkillRoot, "stardew-navigation.md"), "stardew-navigation test guidance");
        var taskContinuityRoot = Path.Combine(gamingSkillRoot, "stardew-task-continuity");
        Directory.CreateDirectory(taskContinuityRoot);
        File.WriteAllText(Path.Combine(taskContinuityRoot, "SKILL.md"), "stardew-task-continuity test guidance");
        var stardewWorldRoot = Path.Combine(gamingSkillRoot, "stardew-world");
        Directory.CreateDirectory(Path.Combine(stardewWorldRoot, "references"));
        File.WriteAllText(
            Path.Combine(stardewWorldRoot, "SKILL.md"),
            "stardew-world test guidance; detailed places live in references/stardew-places.md");
        File.WriteAllText(
            Path.Combine(stardewWorldRoot, "references", "stardew-places.md"),
            "full stardew places encyclopedia fixture");
    }

    private sealed class FakeDiscovery : IStardewBridgeDiscovery
    {
        private readonly StardewBridgeDiscoverySnapshot _snapshot;

        public FakeDiscovery(StardewBridgeDiscoverySnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public string DiscoveryFilePath => "fake-discovery.json";

        public bool TryReadLatest(out StardewBridgeDiscoverySnapshot? snapshot, out string? failureReason)
        {
            snapshot = _snapshot;
            failureReason = null;
            return true;
        }
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

    private sealed class FakeQueryService : IGameQueryService
    {
        private readonly GameObservation _observation;

        public FakeQueryService(GameObservation observation)
        {
            _observation = observation;
        }

        public string? LastNpcId { get; private set; }

        public Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
        {
            LastNpcId = npcId;
            return Task.FromResult(_observation);
        }

        public Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new WorldSnapshot("stardew-valley", "save-42", _observation.TimestampUtc, [], []));
    }

    private sealed class FakeEventSource : IGameEventSource
    {
        private readonly GameEventBatch _batch;

        public FakeEventSource(IReadOnlyList<GameEventRecord> records)
        {
            _batch = new GameEventBatch(records, GameEventCursor.Advance(new GameEventCursor(), records));
        }

        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult(_batch.Records);
    }

    private sealed class CursorAwareEventSource : IGameEventSource
    {
        private readonly GameEventBatch _batch;

        public CursorAwareEventSource(IReadOnlyList<GameEventRecord> records)
        {
            _batch = new GameEventBatch(records, GameEventCursor.Advance(new GameEventCursor(), records));
        }

        public GameEventCursor? SeenCursor { get; private set; }

        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
        {
            SeenCursor = cursor;
            return Task.FromResult(_batch.Records);
        }
    }

    private sealed class FakeCommandService : IGameCommandService
    {
        public GameAction? LastAction { get; private set; }

        public Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
        {
            LastAction = action;
            return Task.FromResult(new GameCommandResult(true, "cmd-1", StardewCommandStatuses.Completed, null, action.TraceId));
        }

        public Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "Penny", "debug", StardewCommandStatuses.Completed, 1, null, null));

        public Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "Penny", "debug", StardewCommandStatuses.Cancelled, 1, reason, null));
    }

    private sealed class ToolSnapshotChatClient : IChatClient
    {
        private readonly string _response;
        private readonly Queue<ChatResponse> _responses;

        public ToolSnapshotChatClient(string response)
        {
            _response = response;
            _responses = new Queue<ChatResponse>();
        }

        public ToolSnapshotChatClient(params ChatResponse[] responses)
        {
            _response = responses.LastOrDefault()?.Content ?? "";
            _responses = new Queue<ChatResponse>(responses);
        }

        public List<string> ToolNames { get; } = new();

        public List<string> SystemMessages { get; } = new();

        public List<string> UserMessages { get; } = new();

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult(_response);

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            ToolNames.Clear();
            ToolNames.AddRange(tools.Select(tool => tool.Name));
            SystemMessages.Clear();
            SystemMessages.AddRange(messages
                .Where(message => string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase))
                .Select(message => message.Content));
            UserMessages.Clear();
            UserMessages.AddRange(messages
                .Where(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                .Select(message => message.Content));

            if (_responses.Count > 0)
                return Task.FromResult(_responses.Dequeue());

            return Task.FromResult(new ChatResponse
            {
                Content = _response,
                FinishReason = "stop"
            });
        }

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

    private sealed class NoopCronScheduler : ICronScheduler
    {
        public event EventHandler<CronTaskDueEventArgs>? TaskDue;

        public void Schedule(CronTask task) => TaskDue?.Invoke(this, new CronTaskDueEventArgs { Task = task, FiredAt = DateTimeOffset.UtcNow });

        public void Cancel(string taskId)
        {
        }

        public CronTask? GetTask(string taskId) => null;

        public IReadOnlyList<CronTask> GetAllTasks() => Array.Empty<CronTask>();

        public DateTimeOffset? GetNextRun(string taskId) => null;
    }

    private sealed class DiscoveredNoopTool : ITool
    {
        public DiscoveredNoopTool(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public string Description => "Dynamic discovered test tool";

        public Type ParametersType => typeof(NoopParameters);

        public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
            => Task.FromResult(ToolResult.Ok("ok"));
    }

    private sealed class NoopParameters
    {
    }
}
