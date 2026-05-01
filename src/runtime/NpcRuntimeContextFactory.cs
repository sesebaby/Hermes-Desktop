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
    private const string DefaultSystemPromptSupplement =
        "You are acting as a Stardew Valley NPC runtime. Observe facts from NPC-local context, " +
        "keep continuity inside this NPC namespace, and use the registered tools available in this session.";

    public NpcRuntimeContextBundle Create(
        NpcNamespace npcNamespace,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        SkillManager skillManager,
        string? systemPromptSupplement = null,
        int maxTokens = 8000,
        int recentTurnWindow = 6,
        bool includeMemory = true,
        bool includeUser = true)
    {
        ArgumentNullException.ThrowIfNull(npcNamespace);
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(skillManager);

        npcNamespace.EnsureDirectories();

        var soulService = npcNamespace.CreateSoulService(loggerFactory.CreateLogger<SoulService>());
        var memoryManager = npcNamespace.CreateMemoryManager(chatClient, loggerFactory.CreateLogger<MemoryManager>());
        var todoStore = new SessionTodoStore();
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
        var promptBuilder = AgentCapabilityAssembler.CreatePromptBuilder(new AgentPromptServices
        {
            SkillManager = skillManager,
            MemoryAvailable = includeMemory || includeUser,
            SupplementalSystemPrompt = string.IsNullOrWhiteSpace(systemPromptSupplement)
                ? DefaultSystemPromptSupplement
                : $"{DefaultSystemPromptSupplement}\n\n{systemPromptSupplement.Trim()}"
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
            new ToolRegistry());
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
    IToolRegistry ToolRegistry);
