using Hermes.Agent.Context;
using Hermes.Agent.LLM;
using Hermes.Agent.Memory;
using Hermes.Agent.Plugins;
using Hermes.Agent.Search;
using Hermes.Agent.Skills;
using Hermes.Agent.Soul;
using Hermes.Agent.Tasks;
using Hermes.Agent.Transcript;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging;

namespace Hermes.Agent.Runtime;

public sealed class NpcRuntimeContextFactory
{
    private const string DefaultInteractiveSystemPromptSupplement =
        "你正在作为星露谷 NPC runtime 行动。使用当前 NPC 自己的上下文和明确的工具结果，" +
        "把连续性保持在这个 NPC namespace 内，并按需要使用本会话注册的工具。";

    private const string DefaultAutonomySystemPromptSupplement =
        "你正在作为星露谷 NPC 自主父层 runtime 行动。宿主只负责唤醒你，不替你选择，也不预载世界事实。" +
        "你是生活在星露谷里的人，要从自己的视角决定下一步行动，把连续性保持在这个 NPC namespace 内，" +
        "并且只返回一个 JSON intent contract。必须返回 raw JSON，不要自然语言解释或 Markdown。" +
        "机械动作由宿主和本地 executor 执行。";

    public NpcRuntimeContextBundle Create(
        NpcNamespace npcNamespace,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        SkillManager skillManager,
        string? channelKey = null,
        string? systemPromptSupplement = null,
        int maxTokens = 8000,
        int recentTurnWindow = 6,
        bool includeMemory = true,
        bool includeUser = true,
        SessionTodoStore? sharedTodoStore = null)
    {
        ArgumentNullException.ThrowIfNull(npcNamespace);
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(skillManager);

        npcNamespace.EnsureDirectories();

        var soulService = npcNamespace.CreateSoulService(loggerFactory.CreateLogger<SoulService>());
        var memoryManager = npcNamespace.CreateMemoryManager(chatClient, loggerFactory.CreateLogger<MemoryManager>());
        var todoStore = sharedTodoStore ?? new SessionTodoStore();
        var taskProjectionService = new SessionTaskProjectionService(todoStore);
        var sessionSearchIndex = npcNamespace.CreateSessionSearchIndex(loggerFactory.CreateLogger<SessionSearchIndex>());
        var transcriptStore = npcNamespace.CreateTranscriptStore(sessionSearchIndex, taskProjectionService);
        var transcriptRecallService = new TranscriptRecallService(
            transcriptStore,
            loggerFactory.CreateLogger<TranscriptRecallService>(),
            sessionSearchIndex,
            chatClient);
        var curatedMemoryProvider = new CuratedMemoryLifecycleProvider(
            memoryManager,
            includeMemory: includeMemory,
            includeUser: includeUser);
        var memoryOrchestrator = new HermesMemoryOrchestrator(
            new IMemoryProvider[]
            {
                curatedMemoryProvider,
                new TranscriptMemoryProvider(transcriptRecallService)
            },
            loggerFactory.CreateLogger<HermesMemoryOrchestrator>(),
            new IMemoryCompressionParticipant[] { curatedMemoryProvider });
        var pluginManager = new PluginManager(loggerFactory.CreateLogger<PluginManager>());
        pluginManager.Register(new BuiltinMemoryPlugin(
            memoryManager,
            includeMemory: includeMemory,
            includeUser: includeUser));
        var isAutonomyChannel = string.Equals(channelKey, "autonomy", StringComparison.OrdinalIgnoreCase);
        var isPrivateChatChannel = string.Equals(channelKey, "private_chat", StringComparison.OrdinalIgnoreCase);
        var promptBuilder = AgentCapabilityAssembler.CreatePromptBuilder(new AgentPromptServices
        {
            SkillManager = skillManager,
            MemoryAvailable = includeMemory || includeUser,
            IncludeSkillsMandatoryCatalog = !isAutonomyChannel && !isPrivateChatChannel,
            UseStardewNpcRuntimePrompt = isAutonomyChannel,
            IncludeMemoryGuidance = !isAutonomyChannel && !isPrivateChatChannel,
            IncludeSessionSearchGuidance = !isAutonomyChannel,
            IncludeSkillsGuidance = !isAutonomyChannel && !isPrivateChatChannel,
            IncludeRuntimeFactsGuidance = !isAutonomyChannel && !isPrivateChatChannel,
            SupplementalSystemPrompt = BuildSystemPromptSupplement(isAutonomyChannel, systemPromptSupplement)
        });
        var contextManager = new ContextManager(
            transcriptStore,
            chatClient,
            new TokenBudget(maxTokens, recentTurnWindow),
            promptBuilder,
            loggerFactory.CreateLogger<ContextManager>(),
            soulService: soulService,
            pluginManager: pluginManager,
            memoryOrchestrator: memoryOrchestrator,
            taskProjectionService: taskProjectionService);
        var turnMemoryCoordinator = new TurnMemoryCoordinator(
            contextManager,
            memoryOrchestrator,
            loggerFactory.CreateLogger<TurnMemoryCoordinator>());
        var memoryReviewService = new MemoryReviewService(
            chatClient,
            memoryManager,
            loggerFactory.CreateLogger<MemoryReviewService>(),
            pluginManager,
            MemoryReviewDefaults.NudgeInterval,
            skillManager,
            MemoryReviewDefaults.SkillCreationNudgeInterval);
        var firstCallContextBudgetPolicy = new StardewAutonomyFirstCallContextBudgetPolicy(
            loggerFactory.CreateLogger<StardewAutonomyFirstCallContextBudgetPolicy>());

        return new NpcRuntimeContextBundle(
            soulService,
            memoryManager,
            transcriptStore,
            promptBuilder,
            contextManager,
            pluginManager,
            transcriptRecallService,
            memoryOrchestrator,
            turnMemoryCoordinator,
            memoryReviewService,
            taskProjectionService,
            todoStore,
            new ToolRegistry(),
            firstCallContextBudgetPolicy,
            firstCallContextBudgetPolicy);
    }

    private static string BuildSystemPromptSupplement(bool isAutonomyChannel, string? systemPromptSupplement)
    {
        var defaultSupplement = isAutonomyChannel
            ? DefaultAutonomySystemPromptSupplement
            : DefaultInteractiveSystemPromptSupplement;
        return string.IsNullOrWhiteSpace(systemPromptSupplement)
            ? defaultSupplement
            : $"{defaultSupplement}\n\n{systemPromptSupplement.Trim()}";
    }
}

public sealed record NpcRuntimeContextBundle(
    SoulService SoulService,
    MemoryManager MemoryManager,
    TranscriptStore TranscriptStore,
    PromptBuilder PromptBuilder,
    ContextManager ContextManager,
    PluginManager PluginManager,
    TranscriptRecallService TranscriptRecallService,
    HermesMemoryOrchestrator MemoryOrchestrator,
    TurnMemoryCoordinator TurnMemoryCoordinator,
    MemoryReviewService MemoryReviewService,
    SessionTaskProjectionService TaskProjectionService,
    SessionTodoStore TodoStore,
    IToolRegistry ToolRegistry,
    Hermes.Agent.Core.IFirstCallContextBudgetPolicy? FirstCallContextBudgetPolicy = null,
    Hermes.Agent.Core.IOutboundContextCompactionPolicy? OutboundContextCompactionPolicy = null);
