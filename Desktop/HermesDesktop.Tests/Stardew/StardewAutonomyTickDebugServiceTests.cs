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
    public async Task RunOneTickAsync_HaleyInjectsRequiredPersonaSkillsAndExcludesLocalExecutorToolsFromParent()
    {
        var chatClient = new ToolSnapshotChatClient(
            """
            {
              "action": "wait",
              "reason": "stay near the library",
              "waitReason": "I will wait near the library.",
              "allowedActions": ["move", "observe", "wait", "task_status"],
              "escalate": false
            }
            """);
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
        Assert.AreEqual("local_executor_completed:wait", result.DecisionResponse);
        Assert.IsNull(queries.LastNpcId);

        Assert.IsTrue(chatClient.ToolNames.Count > 0, "Parent autonomy lane uses a restricted tool surface so tool results stay in its transcript.");
        CollectionAssert.DoesNotContain(chatClient.ToolNames.ToArray(), "stardew_move");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "skill_view");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "stardew_navigate_to_tile");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "stardew_task_status");

        Assert.IsTrue(chatClient.SystemMessages.Any(message =>
            message.Contains("## Persona Facts", StringComparison.Ordinal) &&
            message.Contains("Haley facts", StringComparison.Ordinal) &&
            message.Contains("## Persona Voice", StringComparison.Ordinal) &&
            message.Contains("Haley voice", StringComparison.Ordinal) &&
            message.Contains("## Persona Boundaries", StringComparison.Ordinal) &&
            message.Contains("Haley boundaries", StringComparison.Ordinal) &&
            message.Contains("## Stardew Runtime Contract", StringComparison.Ordinal) &&
            message.Contains("### stardew-core", StringComparison.Ordinal) &&
            message.Contains("### stardew-social", StringComparison.Ordinal) &&
            message.Contains("### stardew-navigation", StringComparison.Ordinal) &&
            message.Contains("### stardew-task-continuity", StringComparison.Ordinal) &&
            message.Contains("### stardew-world", StringComparison.Ordinal) &&
            message.Contains("stardew-world fixture compact contract", StringComparison.Ordinal) &&
            !message.Contains("skill_view(name=\"stardew-world\", file_path=\"references/stardew-places.md\")", StringComparison.Ordinal) &&
            !message.Contains("## Skills (mandatory)", StringComparison.Ordinal) &&
            !message.Contains("full stardew places encyclopedia fixture", StringComparison.Ordinal)));
        Assert.IsTrue(chatClient.SystemMessages.Any(message =>
            message.Contains("宿主不会替你观察，也不会替你选择第一步", StringComparison.Ordinal) &&
            !message.Contains("Decide small next actions from observed game facts", StringComparison.Ordinal) &&
            !message.Contains("Use the available game tools to inspect the world before acting", StringComparison.Ordinal)));

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
    public async Task RunOneTickAsync_WithRepositoryGamingSkillRoot_UsesSkillOwnedCompactContract()
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
                "tile=8,7"
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
            message.Contains("## Stardew Runtime Contract", StringComparison.Ordinal));
        StringAssert.Contains(systemPrompt, "### stardew-world");
        StringAssert.Contains(systemPrompt, "compact-contract-owner: stardew-world");
        StringAssert.Contains(systemPrompt, "skill_view(name=\"stardew-world\", file_path=\"references/stardew-places.md\")");
        StringAssert.Contains(systemPrompt, "### stardew-navigation");
        StringAssert.Contains(systemPrompt, "compact-contract-owner: stardew-navigation");
        StringAssert.Contains(systemPrompt, "target(locationName,x,y,source)");
        StringAssert.Contains(systemPrompt, "`references/index.md`");
        StringAssert.Contains(systemPrompt, "stardew_navigate_to_tile");
        StringAssert.Contains(systemPrompt, "stardew_navigate_to_tile");
        Assert.IsFalse(systemPrompt.Contains("destination[n]", StringComparison.Ordinal));
        Assert.IsFalse(systemPrompt.Contains("stardew_move", StringComparison.Ordinal));
        Assert.IsFalse(systemPrompt.Contains("destinationId", StringComparison.Ordinal));
        Assert.IsTrue(
            chatClient.ToolNames.Contains("stardew_navigate_to_tile", StringComparer.OrdinalIgnoreCase),
            "Mechanical tile navigation must be a parent-callable tool so the tool result stays in the parent transcript.");
        Assert.IsFalse(
            systemPrompt.Contains("stardew-world test guidance", StringComparison.Ordinal),
            "Repo-backed prompt supplement coverage must use the real skills/gaming root, not fixture-only text.");
    }

    [TestMethod]
    public async Task RunOneTickAsync_WithRepositoryGamingSkillRoot_DoesNotUseHostNeedleSelection()
    {
        var repositoryGamingRoot = FindRepositoryPath("skills", "gaming");
        var chatClient = new ToolSnapshotChatClient("I will pick a grounded next action.");
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
                    "Haley is in her room.",
                    ["location=HaleyHouse", "tile=8,7"])),
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
            message.Contains("## Stardew Runtime Contract", StringComparison.Ordinal));
        StringAssert.Contains(systemPrompt, "compact-contract-owner: stardew-world");
        Assert.IsFalse(
            systemPrompt.Contains("这个世界是一个小型的乡村山谷", StringComparison.Ordinal),
            "Compact prompt text must come from the skill-owned compact contract, not host-selected fallback prose.");
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
            message.Contains("## Stardew Runtime Contract", StringComparison.Ordinal));
        StringAssert.Contains(systemPrompt, "### stardew-social");
        StringAssert.Contains(systemPrompt, "`stardew_speak`");
        StringAssert.Contains(systemPrompt, "移动开始、移动到达、闲置、任务状态是主要反馈槽位");
        StringAssert.Contains(systemPrompt, "玩家直接提出的请求优先关注");
        StringAssert.Contains(systemPrompt, "stardew-navigation");
        Assert.IsFalse(systemPrompt.Contains("stardew_move", StringComparison.Ordinal));
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
            message.Contains("## Stardew Runtime Contract", StringComparison.Ordinal));
        StringAssert.Contains(systemPrompt, "### stardew-task-continuity");
        StringAssert.Contains(systemPrompt, "玩家给你以后要兑现的约定时，按角色判断是否接受；接受后用 `todo` 记录短句");
        StringAssert.Contains(systemPrompt, "私聊里答应“现在就做”的现实世界动作，必须用 `npc_delegate_action` 委托");
        StringAssert.Contains(systemPrompt, "stardew_navigate_to_tile");
        StringAssert.Contains(systemPrompt, "stardew_task_status");
        StringAssert.Contains(systemPrompt, "memory");
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
            message.Contains("## Stardew Runtime Contract", StringComparison.Ordinal) &&
            message.Contains("### stardew-core", StringComparison.Ordinal) &&
            message.Contains("### stardew-social", StringComparison.Ordinal) &&
            message.Contains("### stardew-navigation", StringComparison.Ordinal) &&
            !message.Contains("## Skills (mandatory)", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunOneTickAsync_MissingRequiredSkillFileFailsWithSkillIdAndPath()
    {
        File.Delete(Path.Combine(_gamingSkillRoot, "stardew-navigation.md"));
        Directory.Delete(Path.Combine(_gamingSkillRoot, "stardew-navigation"), recursive: true);
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
    public async Task RunOneTickAsync_WithParentMoveIntentDoesNotLetLocalExecutorNavigate()
    {
        var commands = new FakeCommandService();
        var chatClient = new ToolSnapshotChatClient(
            """
            {
              "action": "move",
              "reason": "stand somewhere bright and visible in town",
              "destinationText": "town fountain",
              "escalate": false
            }
            """);
        var delegationChatClient = new ToolSnapshotChatClient(
            new StreamEvent.ToolUseComplete(
                "call-skill-root",
                "skill_view",
                Json("""{"name":"stardew-navigation","file_path":"references/index.md"}""")),
            new StreamEvent.ToolUseComplete(
                "call-nav",
                "stardew_navigate_to_tile",
                Json("""{"locationName":"Town","x":42,"y":17,"source":"test.navigation.town.fountain","facingDirection":2,"reason":"stand somewhere bright and visible in town"}""")));
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            DateTime.UtcNow,
            "Haley is idle in town.",
            [
                "location=Town",
                "tile=42,17",
                "isAvailableForControl=true"
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
            runtimeRoot: _tempDir,
            delegationChatClient: delegationChatClient);

        var result = await service.RunOneTickAsync("Haley", CancellationToken.None);

        Assert.IsTrue(result.Success, result.FailureReason);
        Assert.AreEqual("local_executor_blocked:local_executor_write_action_disabled", result.DecisionResponse);
        Assert.IsFalse(
            chatClient.UserMessages.Any(message => message.Contains("destinationId=town.fountain", StringComparison.Ordinal)),
            "The host must not preload observed destination ids into the parent autonomy prompt.");
        Assert.IsFalse(
            chatClient.UserMessages.Any(message => message.Contains("nearby[0]", StringComparison.Ordinal)),
            "The host must not preload host-generated nearby movement affordances into the parent autonomy prompt.");
        CollectionAssert.DoesNotContain(chatClient.ToolNames.ToArray(), "stardew_move");
        Assert.AreEqual(0, delegationChatClient.ToolNamesByCall.Count);
        CollectionAssert.DoesNotContain(delegationChatClient.ToolNames.ToArray(), "stardew_move");
        CollectionAssert.DoesNotContain(delegationChatClient.ToolNames.ToArray(), "stardew_navigate_to_tile");
        Assert.IsNull(commands.LastAction);
    }

    [TestMethod]
    public async Task RunOneTickAsync_WithPlaceCandidateButNoMoveToolCall_DoesNotMove()
    {
        var commands = new FakeCommandService();
        var delegationChatClient = new ToolSnapshotChatClient(
            new StreamEvent.ToolUseComplete(
                "call-observe",
                "stardew_status",
                Json("{}")));
        var chatClient = new ToolSnapshotChatClient(
            """
            {
              "action": "observe",
              "reason": "look around before deciding whether to move",
              "observeTarget": "Bedroom mirror",
              "allowedActions": ["move", "observe", "wait", "task_status"],
              "escalate": false
            }
            """);
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            DateTime.UtcNow,
            "Haley is in her room.",
            [
                "location=HaleyHouse",
                "tile=8,7",
                "isAvailableForControl=true"
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
            runtimeRoot: _tempDir,
            delegationChatClient: delegationChatClient);

        var result = await service.RunOneTickAsync("Haley", CancellationToken.None);

        Assert.IsTrue(result.Success, result.FailureReason);
        Assert.AreEqual("local_executor_completed:stardew_status", result.DecisionResponse);
        Assert.IsFalse(
            chatClient.UserMessages.Any(message => message.Contains("destination[0]=label=Bedroom mirror", StringComparison.Ordinal)),
            "Wake-only autonomy must not expose place candidates unless the agent explicitly observes through tools.");
        CollectionAssert.Contains(delegationChatClient.ToolNames.ToArray(), "stardew_status");
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
                "isAvailableForControl=true"
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
        Assert.IsFalse(
            chatClient.UserMessages.Any(message =>
                message.Contains("[event] evt-1", StringComparison.Ordinal) &&
                message.Contains("下面的事件只是上下文", StringComparison.Ordinal)),
            "Wake-only autonomy must not inject event records into the parent prompt.");
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

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

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
        File.WriteAllText(
            Path.Combine(gamingSkillRoot, "stardew-core.md"),
            """
            stardew-core test guidance

            ## Compact Contract
            - stardew-core fixture compact contract
            """);
        File.WriteAllText(
            Path.Combine(gamingSkillRoot, "stardew-social.md"),
            """
            stardew-social test guidance

            ## Compact Contract
            - stardew-social fixture compact contract
            """);
        File.WriteAllText(
            Path.Combine(gamingSkillRoot, "stardew-navigation.md"),
            """
            stardew-navigation test guidance

            ## Compact Contract
            - stardew-navigation fixture compact contract
            - `skill_view(name="stardew-navigation", file_path="references/index.md")`
            """);
        var navigationRoot = Path.Combine(gamingSkillRoot, "stardew-navigation");
        Directory.CreateDirectory(Path.Combine(navigationRoot, "references"));
        File.WriteAllText(
            Path.Combine(navigationRoot, "SKILL.md"),
            """
            ---
            name: stardew-navigation
            description: Test navigation skill for parent target resolution.
            ---
            stardew-navigation test guidance

            ## Compact Contract
            - stardew-navigation fixture compact contract
            - `references/index.md`
            """);
        File.WriteAllText(
            Path.Combine(navigationRoot, "references", "index.md"),
            "`target(locationName=Town,x=42,y=17,source=test.navigation.town.fountain)`");
        var taskContinuityRoot = Path.Combine(gamingSkillRoot, "stardew-task-continuity");
        Directory.CreateDirectory(taskContinuityRoot);
        File.WriteAllText(Path.Combine(taskContinuityRoot, "SKILL.md"), "stardew-task-continuity test guidance");
        var stardewWorldRoot = Path.Combine(gamingSkillRoot, "stardew-world");
        Directory.CreateDirectory(Path.Combine(stardewWorldRoot, "references"));
        File.WriteAllText(
            Path.Combine(stardewWorldRoot, "SKILL.md"),
            """
            stardew-world test guidance; detailed places live in references/stardew-places.md

            ## Compact Contract
            - stardew-world fixture compact contract
            """);
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
        private readonly Queue<StreamEvent> _streamEvents;

        public ToolSnapshotChatClient(string response)
        {
            _response = response;
            _responses = new Queue<ChatResponse>();
            _streamEvents = new Queue<StreamEvent>();
        }

        public ToolSnapshotChatClient(params ChatResponse[] responses)
        {
            _response = responses.LastOrDefault()?.Content ?? "";
            _responses = new Queue<ChatResponse>(responses);
            _streamEvents = new Queue<StreamEvent>();
        }

        public ToolSnapshotChatClient(params StreamEvent[] streamEvents)
        {
            _response = "";
            _responses = new Queue<ChatResponse>();
            _streamEvents = new Queue<StreamEvent>(streamEvents);
        }

        public List<string> ToolNames { get; } = new();

        public List<IReadOnlyList<string>> ToolNamesByCall { get; } = [];

        public List<string> SystemMessages { get; } = new();

        public List<string> UserMessages { get; } = new();

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
        {
            ToolNames.Clear();
            SystemMessages.Clear();
            SystemMessages.AddRange(messages
                .Where(message => string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase))
                .Select(message => message.Content));
            UserMessages.Clear();
            UserMessages.AddRange(messages
                .Where(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                .Select(message => message.Content));

            return Task.FromResult(_response);
        }

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
            if (_streamEvents.Count > 0)
            {
                ToolNames.Clear();
                ToolNames.AddRange((tools ?? []).Select(tool => tool.Name));
                ToolNamesByCall.Add(ToolNames.ToArray());
                SystemMessages.Clear();
                if (!string.IsNullOrWhiteSpace(systemPrompt))
                    SystemMessages.Add(systemPrompt);
                UserMessages.Clear();
                UserMessages.AddRange(messages
                    .Where(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                    .Select(message => message.Content));
            }

            while (_streamEvents.Count > 0)
            {
                await Task.Yield();
                yield return _streamEvents.Dequeue();
            }
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
